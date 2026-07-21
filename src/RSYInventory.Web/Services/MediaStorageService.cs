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
        if (category is not ("parts" or "vehicles"))
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

        return relativeDir.Replace('\\', '/') + "/" + fileName;
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
