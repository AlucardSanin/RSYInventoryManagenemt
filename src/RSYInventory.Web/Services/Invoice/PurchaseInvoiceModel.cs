namespace RSYInventory.Web.Services.Invoice;

public sealed class PurchaseInvoiceModel
{
    public int InvoiceNumber { get; init; }
    public DateTime InvoiceDate { get; init; }
    public string? PaymentMethod { get; init; }

    public string CompanyName { get; init; } = string.Empty;
    public string CompanyAddressLine1 { get; init; } = string.Empty;
    public string CompanyCityStateZip { get; init; } = string.Empty;
    public string CompanyEmail { get; init; } = string.Empty;

    public string? BilledToName { get; init; }
    public string? BilledToPhone { get; init; }
    public string? BilledToEmail { get; init; }
    public string? BilledToLocation { get; init; }

    public string ItemDescription { get; init; } = string.Empty;
    public string? ItemVin { get; init; }
    public decimal Amount { get; init; }
}
