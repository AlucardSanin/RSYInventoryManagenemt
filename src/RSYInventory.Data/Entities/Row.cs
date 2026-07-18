using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class Row
{
    public int Id { get; set; }

    public int ZoneId { get; set; }

    public int RowNumber { get; set; }

    public string? Label { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Pallet> Pallets { get; set; } = new List<Pallet>();

    public virtual Zone Zone { get; set; } = null!;
}
