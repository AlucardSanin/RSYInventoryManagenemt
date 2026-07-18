using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Configuration;

/// <summary>
/// Business rules for parts pallets:
/// - Up to 2 transmissions
/// - Not 2 engines (max 1 engine)
/// - Engine + transmission allowed on the same pallet
/// </summary>
public static class PalletCapacityRules
{
    public const int MaxEnginesPerPallet = 1;
    public const int MaxTransmissionsPerPallet = 2;

    public static bool CanPlace(InventoryItemType itemType, int currentEngines, int currentTransmissions)
    {
        return itemType switch
        {
            InventoryItemType.Engine => currentEngines < MaxEnginesPerPallet,
            InventoryItemType.Transmission => currentTransmissions < MaxTransmissionsPerPallet,
            _ => false
        };
    }
}
