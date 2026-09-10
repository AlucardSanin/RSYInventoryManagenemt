namespace RSYInventory.Data.Entities;

/// <summary>
/// Check payment registered against a weekly recycle invoice (one per invoice).
/// </summary>
public partial class RecycleInvoicePayment
{
    public int Id { get; set; }

    public int RecycleWeeklyInvoiceId { get; set; }

    public string CheckImageRelativePath { get; set; } = null!;

    /// <summary>Date printed / written on the check.</summary>
    public DateOnly CheckDate { get; set; }

    public decimal AmountUsd { get; set; }

    public string? Notes { get; set; }

    /// <summary>When the payment was uploaded into the platform.</summary>
    public DateTime RecordedAtUtc { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual RecycleWeeklyInvoice Invoice { get; set; } = null!;

    public virtual User RecordedByUser { get; set; } = null!;
}
