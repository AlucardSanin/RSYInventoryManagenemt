using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Entities;

/// <summary>
/// Movement history / audit log for inventory items (and optionally vehicles via VehicleId).
/// Used to show recent pallet activity by user.
/// </summary>
public class InventoryMovement
{
    public long Id { get; set; }

    public MovementType MovementType { get; set; }

    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public int? VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }

    public int? FromPalletId { get; set; }
    public Pallet? FromPallet { get; set; }

    public int? ToPalletId { get; set; }
    public Pallet? ToPallet { get; set; }

    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string? Notes { get; set; }
    public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;
}
