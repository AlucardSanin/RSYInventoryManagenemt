namespace RSYInventory.Data;

/// <summary>
/// Known vehicle source names and helpers (e.g. auction sources without a private seller).
/// </summary>
public static class VehicleSourceKinds
{
    public const string CopartAuctions = "Copart - Subastas";
    public const string IaaiAuctions = "IAAI - Subastas";

    public static bool IsAuctionSource(string? sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
            return false;

        var n = sourceName.Trim();
        if (n.Equals(CopartAuctions, StringComparison.OrdinalIgnoreCase)
            || n.Equals(IaaiAuctions, StringComparison.OrdinalIgnoreCase))
            return true;

        // Tolerant match if the label is edited slightly in admin/SQL.
        var looksAuction = n.Contains("Subasta", StringComparison.OrdinalIgnoreCase);
        return looksAuction
               && (n.Contains("Copart", StringComparison.OrdinalIgnoreCase)
                   || n.Contains("IAAI", StringComparison.OrdinalIgnoreCase));
    }
}
