using System;

namespace RSYInventory.Data.Entities;

public partial class RecycleWeeklyInvoice
{
    public int Id { get; set; }

    public int InvoiceNumber { get; set; }

    public DateOnly WeekStartDate { get; set; }

    public DateOnly WeekEndDate { get; set; }

    public int BillToCompanyId { get; set; }

    public string PdfRelativePath { get; set; } = null!;

    public decimal TotalAmountUsd { get; set; }

    public int LoadCount { get; set; }

    public DateTime GeneratedAtUtc { get; set; }

    public int GeneratedByUserId { get; set; }

    public DateOnly InvoiceDate { get; set; }

    public virtual RecycleBillToCompany BillToCompany { get; set; } = null!;

    public virtual User GeneratedByUser { get; set; } = null!;

    public virtual RecycleInvoicePayment? Payment { get; set; }
}
