using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class InventoryMovement
{
    public long Id { get; set; }

    public int MovementType { get; set; }

    public int? InventoryItemId { get; set; }

    public int? VehicleId { get; set; }

    public int? FromPalletId { get; set; }

    public int? ToPalletId { get; set; }

    public int UserId { get; set; }

    public string? Notes { get; set; }

    public DateTime MovedAtUtc { get; set; }

    public virtual Pallet? FromPallet { get; set; }

    public virtual InventoryItem? InventoryItem { get; set; }

    public virtual Pallet? ToPallet { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Vehicle? Vehicle { get; set; }
}
