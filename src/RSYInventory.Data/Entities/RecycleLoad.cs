using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class RecycleLoad
{
    public int Id { get; set; }

    /// <summary>External load ID printed on BOL / NUCOR / SCALE papers.</summary>
    public string LoadExternalId { get; set; } = null!;

    public int DriverUserId { get; set; }

    public DateTime RecordedAtUtc { get; set; }

    public int RecordedByUserId { get; set; }

    public bool IsVerified { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public int? VerifiedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual User RecordedByUser { get; set; } = null!;

    public virtual User? VerifiedByUser { get; set; }

    public virtual ICollection<RecycleLoadDocument> Documents { get; set; } = new List<RecycleLoadDocument>();
}
