using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class InventoryItem
{
    public int Id { get; set; }

    public int ItemType { get; set; }

    public int Status { get; set; }

    public string? PartNumber { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public int? Year { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public int? PalletId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual Pallet? Pallet { get; set; }
}
