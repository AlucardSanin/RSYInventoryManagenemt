using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class Zone
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int Purpose { get; set; }

    public int RowAmount { get; set; }

    public int PalletsPerRow { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual ICollection<Row> Rows { get; set; } = new List<Row>();
}
