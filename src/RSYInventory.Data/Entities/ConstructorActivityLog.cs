using System;

namespace RSYInventory.Data.Entities;

public partial class ConstructorActivityLog
{
    public int Id { get; set; }

    public int DriverUserId { get; set; }

    public string RelativePath { get; set; } = null!;

    public DateTime RecordedAtUtc { get; set; }

    public int RecordedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual User RecordedByUser { get; set; } = null!;
}
