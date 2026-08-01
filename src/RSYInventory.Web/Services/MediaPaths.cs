namespace RSYInventory.Web.Services;

/// <summary>
/// Normalizes stored upload paths for &lt;img src&gt; so they resolve from site root
/// (avoids /vehicles/uploads/... when the page is under /vehicles).
/// </summary>
public static class MediaPaths
{
    public static string? ToUrl(string? relativeOrAbsolutePath)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolutePath))
            return null;

        var path = relativeOrAbsolutePath.Trim().Replace('\\', '/');
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        return path.StartsWith('/') ? path : "/" + path;
    }

    public static IReadOnlyList<string> ToUrls(IEnumerable<string?> paths)
        => paths
            .Select(ToUrl)
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToList();
}
