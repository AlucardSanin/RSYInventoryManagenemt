namespace RSYInventory.Data.Configuration;

/// <summary>
/// Structural rules for zones, rows, and pallets.
/// Enforcement will live in application services; these helpers centralize the checks.
/// </summary>
public static class YardLayoutRules
{
    /// <summary>
    /// A zone, row, or pallet cannot be deleted while it still contains inventory or vehicles.
    /// </summary>
    public static bool CanDeleteLocation(int availableInventoryCount, int vehicleCount)
        => availableInventoryCount == 0 && vehicleCount == 0;

    public static string DeletionBlockedMessage
        => "No se puede eliminar una zona, fila o paleta que contenga inventario o vehículos.";
}
