namespace RSYInventory.Web.Services.Invoice;

/// <summary>Data for the vehicle purchase acknowledgement / bill of sale (RSY is the buyer).</summary>
public sealed class PurchaseInvoiceModel
{
    public int DocumentNumber { get; init; }
    public DateTime PurchaseDate { get; init; }
    public string? PaymentMethod { get; init; }

    public string BuyerName { get; init; } = string.Empty;
    public string BuyerAuthorizedName { get; init; } = "RODRIGUEZ SALVAGE YARD";
    public string BuyerAddressLine1 { get; init; } = string.Empty;
    public string BuyerCityStateZip { get; init; } = string.Empty;
    public string BuyerEmail { get; init; } = string.Empty;
    public string? TemplateName { get; init; }

    public string? SellerName { get; init; }
    public string? SellerPhone { get; init; }
    public string? SellerEmail { get; init; }
    public string? SellerAddress { get; init; }

    public int? VehicleYear { get; init; }
    public string? VehicleMake { get; init; }
    public string? VehicleModel { get; init; }
    public string? VehicleVin { get; init; }
    public string VehicleDescription { get; init; } = string.Empty;

    public decimal PurchasePrice { get; init; }

    // Kept for callers that still use InvoiceNumber naming.
    public int InvoiceNumber => DocumentNumber;
    public DateTime InvoiceDate => PurchaseDate;
    public decimal Amount => PurchasePrice;
    public string ItemDescription => VehicleDescription;
    public string? ItemVin => VehicleVin;
    public string? BilledToName => SellerName;
    public string? BilledToPhone => SellerPhone;
    public string? BilledToEmail => SellerEmail;
    public string? BilledToLocation => SellerAddress;
}
