namespace RSYInventory.Data.Enums;

/// <summary>
/// Display status for a recycle weekly invoice payment (check).
/// </summary>
public enum RecycleInvoicePaymentStatus
{
    Pending = 0,
    Paid = 1,
    AmountMismatch = 2
}
