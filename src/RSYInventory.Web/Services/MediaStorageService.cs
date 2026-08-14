using Microsoft.AspNetCore.Hosting;

namespace RSYInventory.Web.Services;

/// <summary>
/// Stores images under wwwroot/uploads/{parts|vehicles}/{brand}/{id}/.
/// Paths are relative and work the same on Windows IIS and Linux VPS.
/// </summary>
public sealed class MediaStorageService(IWebHostEnvironment env)
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public async Task<string> SaveAsync(
        Stream content,
        string originalFileName,
        string category,
        int entityId,
        string? brandOrMake,
        CancellationToken cancellationToken = default)
    {
        if (category is not ("parts" or "vehicles" or "pickups" or "recycle" or "constructor"))
            throw new InvalidOperationException("Categoría de imagen inválida.");

        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
            throw new InvalidOperationException("Solo se permiten imágenes JPG, PNG o WEBP.");

        var brandFolder = SanitizeFolder(brandOrMake) ?? "_sin-marca";
        var relativeDir = Path.Combine("uploads", category, brandFolder, entityId.ToString());
        var absoluteDir = Path.Combine(env.WebRootPath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        // Unique file name so multiple photos can live in the same folder.
        var fileName = $"photo-{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var absolutePath = Path.Combine(absoluteDir, fileName);
        await using (var fs = File.Create(absolutePath))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        return "/" + relativeDir.Replace('\\', '/') + "/" + fileName;
    }

    /// <summary>Stores a PDF (or similar) under wwwroot/uploads/{category}/{entityId}/.</summary>
    public async Task<string> SaveDocumentAsync(
        Stream content,
        string originalFileName,
        string category,
        int entityId,
        CancellationToken cancellationToken = default)
    {
        if (category is not ("invoice-templates" or "vehicle-invoices" or "recycle-invoices"))
            throw new InvalidOperationException("Categoría de documento inválida.");

        var ext = Path.GetExtension(originalFileName);
        if (!string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Solo se permiten archivos PDF.");

        var relativeDir = Path.Combine("uploads", category, entityId.ToString());
        var absoluteDir = Path.Combine(env.WebRootPath, relativeDir);
        Directory.CreateDirectory(absoluteDir);

        var fileName = category switch
        {
            "vehicle-invoices" => Path.GetFileName(originalFileName),
            "recycle-invoices" => Path.GetFileName(originalFileName),
            _ => $"template-{Guid.NewGuid():N}.pdf"
        };

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            fileName = $"document-{Guid.NewGuid():N}.pdf";

        var absolutePath = Path.Combine(absoluteDir, fileName);
        await using (var fs = File.Create(absolutePath))
        {
            await content.CopyToAsync(fs, cancellationToken);
        }

        return "/" + relativeDir.Replace('\\', '/') + "/" + fileName;
    }

    public async Task<string> SaveRecycleInvoicePdfAsync(
        int invoiceNumber,
        byte[] pdfBytes,
        CancellationToken cancellationToken = default)
    {
        var fileName = $"recycle-invoice-{invoiceNumber}.pdf";
        await using var stream = new MemoryStream(pdfBytes);
        return await SaveDocumentAsync(stream, fileName, "recycle-invoices", invoiceNumber, cancellationToken);
    }

    public async Task<string> SaveVehicleInvoicePdfAsync(
        int vehicleId,
        int invoiceNumber,
        byte[] pdfBytes,
        bool signed,
        CancellationToken cancellationToken = default)
    {
        var fileName = signed
            ? $"Purchase-Acknowledgement-{invoiceNumber}-signed.pdf"
            : $"Purchase-Acknowledgement-{invoiceNumber}.pdf";
        await using var stream = new MemoryStream(pdfBytes);
        return await SaveDocumentAsync(stream, fileName, "vehicle-invoices", vehicleId, cancellationToken);
    }

    public byte[]? TryReadBytes(string? relativeWebPath)
    {
        var absolute = ToAbsolutePath(relativeWebPath);
        if (absolute is null || !File.Exists(absolute))
            return null;
        return File.ReadAllBytes(absolute);
    }

    public void TryDelete(string? relativeWebPath)
    {
        var absolute = ToAbsolutePath(relativeWebPath);
        if (absolute is null || !File.Exists(absolute))
            return;
        try { File.Delete(absolute); }
        catch { /* best effort */ }
    }

    private string? ToAbsolutePath(string? relativeWebPath)
    {
        if (string.IsNullOrWhiteSpace(relativeWebPath))
            return null;

        var trimmed = relativeWebPath.Trim().TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(env.WebRootPath, trimmed);
    }

    private static string? SanitizeFolder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var cleaned = new string(value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);
        cleaned = cleaned.Trim('-');
        return string.IsNullOrEmpty(cleaned) ? null : cleaned[..Math.Min(cleaned.Length, 40)];
    }
}
