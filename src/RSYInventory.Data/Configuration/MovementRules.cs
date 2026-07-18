using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Configuration;

/// <summary>
/// When changing an item location, ask whether it was Sold or only Relocated,
/// and always write an InventoryMovement log entry.
/// </summary>
public static class MovementRules
{
    public static bool RequiresSoldOrRelocateChoice(MovementType type)
        => type is MovementType.Sold or MovementType.Relocated;

    public static bool ClearsPalletOnSale(MovementType type)
        => type == MovementType.Sold;
}
