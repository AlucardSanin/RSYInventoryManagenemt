namespace RSYInventory.Data.Entities;

/// <summary>
/// Branding / layout profile for purchase acknowledgements.
/// Multiple companies can issue receipts; admin can add more later (including uploaded PDFs).
/// </summary>
public partial class InvoiceTemplate
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string BuyerCompanyName { get; set; } = string.Empty;

    /// <summary>Printed on buyer signature / printed-name lines.</summary>
    public string BuyerAuthorizedName { get; set; } = string.Empty;

    public string AddressLine1 { get; set; } = string.Empty;

    public string CityStateZip { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>0 = QuestPDF layout; 1 = uploaded PDF (future).</summary>
    public int TemplateKind { get; set; }

    public string? PdfRelativePath { get; set; }

    public string? LogoRelativePath { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
