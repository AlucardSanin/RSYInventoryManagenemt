using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace RSYInventory.Web.Services.Invoice;

public sealed class DocuSealClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly DocuSealOptions _options;

    public DocuSealClient(HttpClient http, IOptions<DocuSealOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public bool IsConfigured =>
        _options.Enabled
        && !string.IsNullOrWhiteSpace(_options.ApiKey)
        && !string.IsNullOrWhiteSpace(_options.BaseUrl);

    /// <summary>
    /// Free DocuSeal OSS only supports submissions from an existing template.
    /// Per-vehicle data is passed as prefilled field values (not via /submissions/pdf, which is Pro).
    /// </summary>
    public async Task<DocuSealSubmissionResult> CreatePurchaseReceiptSubmissionAsync(
        PurchaseInvoiceModel model,
        string sellerEmail,
        string? sellerName,
        int? docuSealTemplateId = null,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "DocuSeal no está configurado. Completa DocuSeal:Enabled, BaseUrl y ApiKey.");

        var templateId = docuSealTemplateId is > 0
            ? docuSealTemplateId.Value
            : _options.TemplateId;

        if (templateId <= 0)
            throw new InvalidOperationException(
                "Falta el TemplateId de DocuSeal para esta plantilla. Configúralo en Administración → Plantillas de recibo.");

        if (string.IsNullOrWhiteSpace(sellerEmail))
            throw new InvalidOperationException("El vendedor no tiene correo para firmar.");

        var sellerRole = string.IsNullOrWhiteSpace(_options.SellerRole)
            ? DocuSealReceiptFields.DefaultSellerRole
            : _options.SellerRole.Trim();

        var values = DocuSealReceiptFields.BuildPrefillValues(model);
        var fields = values.Select(kv => new
        {
            name = kv.Key,
            default_value = kv.Value?.ToString() ?? "",
            @readonly = true
        }).ToArray();

        var payload = new
        {
            template_id = templateId,
            send_email = _options.SendEmail,
            order = "preserved",
            submitters = new object[]
            {
                new
                {
                    role = sellerRole,
                    email = sellerEmail.Trim(),
                    name = string.IsNullOrWhiteSpace(sellerName) ? sellerEmail.Trim() : sellerName.Trim(),
                    values,
                    fields
                }
            }
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/submissions")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Auth-Token", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DocuSeal error {(int)response.StatusCode}: {raw}");

        var submitters = JsonSerializer.Deserialize<List<DocuSealSubmitterDto>>(raw, JsonOptions)
            ?? throw new InvalidOperationException("DocuSeal no devolvió submitters.");

        var seller = submitters.FirstOrDefault(s =>
                        string.Equals(s.Role, sellerRole, StringComparison.OrdinalIgnoreCase))
                    ?? submitters.FirstOrDefault();

        if (seller is null || string.IsNullOrWhiteSpace(seller.EmbedSrc))
            throw new InvalidOperationException("DocuSeal no devolvió el enlace de firma del vendedor.");

        return new DocuSealSubmissionResult(
            seller.SubmissionId ?? 0,
            seller.EmbedSrc!,
            seller.Email ?? sellerEmail,
            seller.Status ?? "awaiting");
    }

    public async Task<DocuSealSubmissionStatus> GetSubmissionAsync(int submissionId, CancellationToken ct = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/submissions/{submissionId}");
        request.Headers.TryAddWithoutValidation("X-Auth-Token", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DocuSeal error {(int)response.StatusCode}: {raw}");

        var dto = JsonSerializer.Deserialize<DocuSealSubmissionDto>(raw, JsonOptions)
            ?? throw new InvalidOperationException("DocuSeal no devolvió el envío.");

        return new DocuSealSubmissionStatus(
            dto.Id,
            dto.Status ?? "unknown",
            dto.CombinedDocumentUrl);
    }

    public async Task<byte[]> DownloadSignedPdfAsync(int submissionId, CancellationToken ct = default)
    {
        EnsureConfigured();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/submissions/{submissionId}/documents?merge=true");
        request.Headers.TryAddWithoutValidation("X-Auth-Token", _options.ApiKey);

        using var response = await _http.SendAsync(request, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"DocuSeal error {(int)response.StatusCode}: {raw}");

        var docs = JsonSerializer.Deserialize<DocuSealDocumentsDto>(raw, JsonOptions);
        var url = docs?.Documents?.FirstOrDefault()?.Url
                  ?? docs?.Url;

        if (string.IsNullOrWhiteSpace(url))
        {
            var status = await GetSubmissionAsync(submissionId, ct);
            url = status.CombinedDocumentUrl;
        }

        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException(
                "DocuSeal aún no tiene el PDF firmado. Espera a que el vendedor complete la firma.");

        using var fileRequest = new HttpRequestMessage(HttpMethod.Get, url);
        fileRequest.Headers.TryAddWithoutValidation("X-Auth-Token", _options.ApiKey);
        using var fileResponse = await _http.SendAsync(fileRequest, ct);
        if (!fileResponse.IsSuccessStatusCode)
        {
            var err = await fileResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"No se pudo descargar el PDF firmado ({(int)fileResponse.StatusCode}): {err}");
        }

        return await fileResponse.Content.ReadAsByteArrayAsync(ct);
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "DocuSeal no está configurado. Completa DocuSeal:Enabled, BaseUrl y ApiKey.");
    }
}

    /// <summary>Field names must match the DocuSeal master template (DOCX tags).</summary>
public static class DocuSealReceiptFields
{
    public const string DocumentNumber = "DocumentNumber";
    public const string PurchaseDate = "PurchaseDate";
    public const string VehicleYmm = "VehicleYMM";
    public const string Vin = "VIN";
    public const string SellerName = "SellerName";
    public const string SellerAddress = "SellerAddress";
    public const string SellerPhone = "SellerPhone";
    public const string SellerEmail = "SellerEmail";
    public const string BuyerName = "BuyerName";
    public const string BuyerAddress = "BuyerAddress";
    public const string BuyerEmail = "BuyerEmail";
    public const string Description = "Description";
    public const string Price = "Price";
    public const string Amount = "Amount";
    public const string PaymentMethod = "PaymentMethod";
    public const string SellerSignature = "SellerSignature";
    public const string SellerDate = "SellerDate";

    public const string DefaultSellerRole = "Seller";

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static Dictionary<string, object> BuildPrefillValues(PurchaseInvoiceModel model)
    {
        var money = model.PurchasePrice.ToString("C", Us);
        var docNo = model.DocumentNumber.ToString("000000", Us);
        var ymm = string.Join(" / ", new[]
        {
            model.VehicleYear?.ToString(Us),
            model.VehicleMake,
            model.VehicleModel
        }.Select(s => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim()));

        var buyerAddress = string.Join(", ", new[]
        {
            model.BuyerAddressLine1,
            model.BuyerCityStateZip
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // Canonical names + common aliases (if DocuSeal fields were named from the old guide).
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [DocumentNumber] = docNo,
            ["No.DocumentNumber"] = docNo,
            ["NO.DocumentNumber"] = docNo,
            ["Document Number"] = docNo,
            [PurchaseDate] = model.PurchaseDate.ToString("MMMM d, yyyy", Us),
            [VehicleYmm] = ymm,
            [Vin] = model.VehicleVin?.Trim() ?? "",
            [SellerName] = model.SellerName?.Trim() ?? "",
            [SellerAddress] = model.SellerAddress?.Trim() ?? "",
            [SellerPhone] = model.SellerPhone?.Trim() ?? "",
            [SellerEmail] = model.SellerEmail?.Trim() ?? "",
            [BuyerName] = model.BuyerName?.Trim() ?? "",
            [BuyerAddress] = buyerAddress,
            [BuyerEmail] = model.BuyerEmail?.Trim() ?? "",
            [Description] = $"Purchase of {model.VehicleDescription}",
            [Price] = money,
            [Amount] = money,
            ["SalePrice"] = money,
            ["Sale price"] = money,
            [PaymentMethod] = model.PaymentMethod?.Trim() ?? ""
        };
    }

    public static string TextTag(string fieldName, string type, string role, bool readOnly = false)
    {
        var readonlyPart = readOnly ? ";readonly=true" : "";
        return $"{{{{{fieldName};type={type};role={role}{readonlyPart}}}}}";
    }
}

public sealed record DocuSealSubmissionResult(
    int SubmissionId,
    string SellerSigningUrl,
    string SellerEmail,
    string Status);

public sealed record DocuSealSubmissionStatus(
    int SubmissionId,
    string Status,
    string? CombinedDocumentUrl);

internal sealed class DocuSealSubmitterDto
{
    public int Id { get; set; }
    public int? SubmissionId { get; set; }
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Status { get; set; }
    public string? EmbedSrc { get; set; }
}

internal sealed class DocuSealSubmissionDto
{
    public int Id { get; set; }
    public string? Status { get; set; }
    public string? CombinedDocumentUrl { get; set; }
}

internal sealed class DocuSealDocumentsDto
{
    public int? Id { get; set; }
    public string? Url { get; set; }
    public List<DocuSealDocumentItemDto>? Documents { get; set; }
}

internal sealed class DocuSealDocumentItemDto
{
    public string? Name { get; set; }
    public string? Url { get; set; }
}
