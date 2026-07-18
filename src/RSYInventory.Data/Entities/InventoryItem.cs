using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Entities;

/// <summary>
/// Engine or transmission stored on a pallet in a parts zone.
/// </summary>
public class InventoryItem
{
    public int Id { get; set; }
    public InventoryItemType ItemType { get; set; }
    public InventoryItemStatus Status { get; set; } = InventoryItemStatus.Available;

    public string? PartNumber { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    /// <summary>Null when sold or not yet placed.</summary>
    public int? PalletId { get; set; }
    public Pallet? Pallet { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
