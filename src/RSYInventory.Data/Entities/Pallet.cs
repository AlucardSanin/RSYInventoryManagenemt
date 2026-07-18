namespace RSYInventory.Data.Entities;

/// <summary>
/// Storage unit. Capacity rules for parts zones:
/// - Max 1 engine
/// - Max 2 transmissions
/// - Engine + transmission(s) allowed on the same pallet
/// </summary>
public class Pallet
{
    public int Id { get; set; }
    public int RowId { get; set; }
    public Row Row { get; set; } = null!;

    /// <summary>1-based pallet number within the row.</summary>
    public int PalletNumber { get; set; }
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<InventoryMovement> MovementsFrom { get; set; } = new List<InventoryMovement>();
    public ICollection<InventoryMovement> MovementsTo { get; set; } = new List<InventoryMovement>();
}
