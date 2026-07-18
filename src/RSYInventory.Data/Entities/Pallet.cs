using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class Pallet
{
    public int Id { get; set; }

    public int RowId { get; set; }

    public int PalletNumber { get; set; }

    public string? Label { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();

    public virtual ICollection<InventoryMovement> InventoryMovementFromPallets { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<InventoryMovement> InventoryMovementToPallets { get; set; } = new List<InventoryMovement>();

    public virtual Row Row { get; set; } = null!;

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
