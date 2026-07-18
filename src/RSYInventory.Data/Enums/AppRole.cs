namespace RSYInventory.Data.Enums;

/// <summary>
/// Application roles. Higher privileges imply inventory edit rights where noted.
/// </summary>
public enum AppRole
{
    /// <summary>Can only view inventory and locations.</summary>
    InventoryViewer = 1,

    /// <summary>Can add, edit, and remove inventory; can assign vehicle locations.</summary>
    InventoryEditor = 2,

    /// <summary>Can create/edit zones, rows, and pallets (parts and vehicles). Includes inventory edit rights.</summary>
    ZoneManager = 3,

    /// <summary>Can register newly acquired vehicles (VIN, source, etc.). Does not assign yard location.</summary>
    VehicleAcquirer = 4
}
