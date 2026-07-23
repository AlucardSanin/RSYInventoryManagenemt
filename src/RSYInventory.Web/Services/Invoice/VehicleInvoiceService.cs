using RSYInventory.Data.Entities;
using RSYInventory.Data.Services;
using RSYInventory.Web.Services;

namespace RSYInventory.Web.Services.Invoice;

public sealed class VehicleInvoiceService
{
    private readonly VehicleService _vehicles;
    private readonly InvoiceTemplateService _templates;
    private readonly PurchaseInvoicePdfService _pdf;
    private readonly DocuSealMasterTemplateService _docuSealMaster;
    private readonly InvoiceEmailService _email;
    private readonly DocuSealClient _docuSeal;
    private readonly MediaStorageService _media;
    private readonly AuthService _auth;
    private readonly AuditService _audit;

    public VehicleInvoiceService(
        VehicleService vehicles,
        InvoiceTemplateService templates,
        PurchaseInvoicePdfService pdf,
        DocuSealMasterTemplateService docuSealMaster,
        InvoiceEmailService email,
        DocuSealClient docuSeal,
        MediaStorageService media,
        AuthService auth,
        AuditService audit)
    {
        _vehicles = vehicles;
        _templates = templates;
        _pdf = pdf;
        _docuSealMaster = docuSealMaster;
        _email = email;
        _docuSeal = docuSeal;
        _media = media;
        _auth = auth;
        _audit = audit;
    }

    public bool CanEmail => _email.IsConfigured;

    public bool CanSendForSignature => _docuSeal.IsConfigured;

    public Task<IReadOnlyList<InvoiceTemplate>> ListTemplatesAsync(CancellationToken ct = default)
        => _templates.ListActiveAsync(ct);

    public byte[] GetDocuSealMasterTemplateDocx()
        => _docuSealMaster.GenerateDocx();

    /// <summary>
    /// Loads an existing receipt for the vehicle (signed preferred), refreshing DocuSeal if needed.
    /// Returns null when there is no persisted receipt yet.
    /// </summary>
    public async Task<VehicleInvoiceResult?> TryLoadExistingAsync(int vehicleId, CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        if (vehicle.InvoiceNumber is not > 0)
            return null;

        if (vehicle.SignedAtUtc is null
            && vehicle.DocuSealSubmissionId is int submissionId
            && _docuSeal.IsConfigured)
        {
            try
            {
                vehicle = await RefreshSignedFromDocuSealAsync(vehicle, submissionId, ct);
            }
            catch
            {
                // Keep showing unsigned / awaiting if DocuSeal is unreachable.
            }
        }

        InvoiceTemplate? template = null;
        if (vehicle.InvoiceTemplateId is int templateId)
            template = await _templates.GetByIdAnyAsync(templateId, ct);

        template ??= await _templates.GetDefaultAsync(ct);

        byte[]? pdfBytes = null;
        var isSigned = vehicle.SignedAtUtc is not null
                       || !string.IsNullOrWhiteSpace(vehicle.SignedPdfRelativePath);

        if (isSigned)
            pdfBytes = _media.TryReadBytes(vehicle.SignedPdfRelativePath);

        if (pdfBytes is null)
            pdfBytes = _media.TryReadBytes(vehicle.UnsignedPdfRelativePath);

        // Legacy receipts: number assigned but no files on disk yet — regenerate from data without changing number.
        if (pdfBytes is null)
        {
            var model = _pdf.BuildModel(vehicle, vehicle.InvoiceNumber!.Value, template);
            pdfBytes = _pdf.GeneratePdf(model);
            var path = await _media.SaveVehicleInvoicePdfAsync(
                vehicle.Id, vehicle.InvoiceNumber.Value, pdfBytes, signed: false, ct);
            await _vehicles.SaveInvoiceDraftAsync(vehicle.Id, template.Id, path, ct);
            vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
                ?? throw new InvalidOperationException("Vehículo no encontrado.");
            isSigned = false;
        }

        var documentNumber = vehicle.InvoiceNumber!.Value;
        var built = _pdf.BuildModel(vehicle, documentNumber, template);
        var fileName = isSigned
            ? $"Purchase-Acknowledgement-{documentNumber}-signed.pdf"
            : $"Purchase-Acknowledgement-{documentNumber}.pdf";

        return new VehicleInvoiceResult(vehicle, built, pdfBytes, fileName, template, isSigned);
    }

    public async Task<VehicleInvoiceResult> GenerateAsync(
        int vehicleId,
        int? templateId = null,
        CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var template = templateId is int id
            ? await _templates.GetByIdAsync(id, ct)
              ?? throw new InvalidOperationException("Plantilla no encontrada o inactiva.")
            : await _templates.GetDefaultAsync(ct);

        var invoiceNumber = await _vehicles.EnsureInvoiceNumberAsync(vehicleId, ct);

        vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var model = _pdf.BuildModel(vehicle, invoiceNumber, template);
        var pdfBytes = _pdf.GeneratePdf(model);
        var fileName = $"Purchase-Acknowledgement-{invoiceNumber}.pdf";

        _media.TryDelete(vehicle.UnsignedPdfRelativePath);
        _media.TryDelete(vehicle.SignedPdfRelativePath);

        var relativePath = await _media.SaveVehicleInvoicePdfAsync(
            vehicleId, invoiceNumber, pdfBytes, signed: false, ct);
        await _vehicles.SaveInvoiceDraftAsync(vehicleId, template.Id, relativePath, ct);

        vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        await _audit.WriteAsync(
            "InvoiceGenerated",
            "Vehicle",
            vehicleId,
            $"Recibo #{invoiceNumber} generado (plantilla {template.Name})",
            $"TemplateId={template.Id}; Path={relativePath}",
            ct);

        return new VehicleInvoiceResult(vehicle, model, pdfBytes, fileName, template, IsSigned: false);
    }

    public async Task BeginRegenerateAsync(int vehicleId, string adminPassword, CancellationToken ct = default)
    {
        await _auth.VerifySystemAdminPasswordAsync(adminPassword, ct);

        var vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var previousNumber = vehicle.InvoiceNumber;
        _media.TryDelete(vehicle.UnsignedPdfRelativePath);
        _media.TryDelete(vehicle.SignedPdfRelativePath);
        await _vehicles.ClearInvoiceForRegenerateAsync(vehicleId, ct);

        await _audit.WriteAsync(
            "InvoiceRegenerateAuthorized",
            "Vehicle",
            vehicleId,
            $"Regeneración autorizada del recibo #{previousNumber?.ToString() ?? "—"}",
            $"DocuSealSubmissionId={vehicle.DocuSealSubmissionId}",
            ct);
    }

    public async Task SendSignedEmailAsync(VehicleInvoiceResult invoice, CancellationToken ct = default)
    {
        if (!invoice.IsSigned)
            throw new InvalidOperationException(
                "El recibo aún no está firmado. Usa «Enviar enlace de firma» primero y espera la firma.");

        var pdf = _media.TryReadBytes(invoice.Vehicle.SignedPdfRelativePath) ?? invoice.PdfBytes;

        await _email.SendInvoiceAsync(
            invoice.Vehicle.SellerEmail ?? string.Empty,
            invoice.Vehicle.SellerName,
            invoice.Model.DocumentNumber,
            pdf,
            invoice.FileName,
            ct);

        await _audit.WriteAsync(
            "SignedInvoiceEmailed",
            "Vehicle",
            invoice.Vehicle.Id,
            $"Recibo firmado #{invoice.Model.DocumentNumber} enviado a {invoice.Vehicle.SellerEmail}",
            null,
            ct);
    }

    public async Task<DocuSealSubmissionResult> SendSigningLinkAsync(
        VehicleInvoiceResult invoice,
        CancellationToken ct = default)
    {
        var vehicle = invoice.Vehicle;

        var result = await _docuSeal.CreatePurchaseReceiptSubmissionAsync(
            invoice.Model,
            vehicle.SellerEmail ?? string.Empty,
            vehicle.SellerName,
            ct);

        await _vehicles.SaveSignatureSentAsync(
            vehicle.Id, result.SubmissionId, result.SellerSigningUrl, ct);

        if (_email.IsConfigured)
        {
            await _email.SendSigningLinkAsync(
                vehicle.SellerEmail ?? string.Empty,
                vehicle.SellerName,
                invoice.Model.DocumentNumber,
                result.SellerSigningUrl,
                ct);
        }

        await _audit.WriteAsync(
            "SignatureLinkSent",
            "Vehicle",
            vehicle.Id,
            $"Enlace de firma enviado para recibo #{invoice.Model.DocumentNumber}",
            $"SubmissionId={result.SubmissionId}; Email={vehicle.SellerEmail}",
            ct);

        return result;
    }

    public async Task<VehicleInvoiceResult> RefreshSignatureStatusAsync(
        int vehicleId,
        CancellationToken ct = default)
    {
        var vehicle = await _vehicles.GetByIdAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        if (vehicle.DocuSealSubmissionId is not int submissionId)
            throw new InvalidOperationException("Este recibo aún no tiene un envío de firma.");

        vehicle = await RefreshSignedFromDocuSealAsync(vehicle, submissionId, ct);
        return await TryLoadExistingAsync(vehicleId, ct)
            ?? throw new InvalidOperationException("No se pudo cargar el recibo actualizado.");
    }

    private async Task<Vehicle> RefreshSignedFromDocuSealAsync(
        Vehicle vehicle,
        int submissionId,
        CancellationToken ct)
    {
        var status = await _docuSeal.GetSubmissionAsync(submissionId, ct);
        if (!string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase))
            return vehicle;

        var signedBytes = await _docuSeal.DownloadSignedPdfAsync(submissionId, ct);
        var invoiceNumber = vehicle.InvoiceNumber
            ?? throw new InvalidOperationException("El vehículo no tiene número de recibo.");

        _media.TryDelete(vehicle.SignedPdfRelativePath);
        var path = await _media.SaveVehicleInvoicePdfAsync(
            vehicle.Id, invoiceNumber, signedBytes, signed: true, ct);
        await _vehicles.SaveSignedInvoiceAsync(vehicle.Id, path, ct);

        await _audit.WriteAsync(
            "InvoiceSigned",
            "Vehicle",
            vehicle.Id,
            $"Recibo #{invoiceNumber} firmado (DocuSeal #{submissionId})",
            $"Path={path}",
            ct);

        return await _vehicles.GetByIdAsync(vehicle.Id, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");
    }
}

public sealed record VehicleInvoiceResult(
    Vehicle Vehicle,
    PurchaseInvoiceModel Model,
    byte[] PdfBytes,
    string FileName,
    InvoiceTemplate Template,
    bool IsSigned);
