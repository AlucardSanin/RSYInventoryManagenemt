using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class RecycleLoad
{
    public int Id { get; set; }

    /// <summary>External load ID printed on BOL / NUCOR / SCALE papers (ticket).</summary>
    public string LoadExternalId { get; set; } = null!;

    public int DriverUserId { get; set; }

    /// <summary>Company this load was sold to (BILL TO on weekly invoice).</summary>
    public int BillToCompanyId { get; set; }

    /// <summary>Business date of the load (Eastern calendar day).</summary>
    public DateOnly LoadDate { get; set; }

    /// <summary>Per-load rate in USD (default 575).</summary>
    public decimal RateUsd { get; set; } = 575m;

    public string? TruckNumber { get; set; }

    public DateTime RecordedAtUtc { get; set; }

    public int RecordedByUserId { get; set; }

    public bool IsVerified { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public int? VerifiedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual User Driver { get; set; } = null!;

    public virtual RecycleBillToCompany BillToCompany { get; set; } = null!;

    public virtual User RecordedByUser { get; set; } = null!;

    public virtual User? VerifiedByUser { get; set; }

    public virtual ICollection<RecycleLoadDocument> Documents { get; set; } = new List<RecycleLoadDocument>();
}
