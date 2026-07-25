namespace RSYInventory.Web.Services.Invoice;

public sealed class CompanyInvoiceOptions
{
    public const string SectionName = "Invoice:Company";

    public string Name { get; set; } = "Rodriguez Salvage Yard, CORP";
    public string AddressLine1 { get; set; } = "4417 US-70 BUS";
    public string CityStateZip { get; set; } = "Clayton, NC, 27520";
    public string Email { get; set; } = "rodriguezyardclayton@gmail.com";
    public string? Phone { get; set; }
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "rodriguezyardclayton@gmail.com";
    public string FromDisplayName { get; set; } = "Rodriguez Salvage Yard";
}

public sealed class DocuSealOptions
{
    public const string SectionName = "DocuSeal";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://166.1.85.41:8080";
    public string ApiKey { get; set; } = string.Empty;
    public int TemplateId { get; set; } = 1;
    /// <summary>Must match the role on fields in the DocuSeal master template.</summary>
    public string SellerRole { get; set; } = DocuSealReceiptFields.DefaultSellerRole;
    public string BuyerRole { get; set; } = "Segunda Parte";
    public string BuyerEmail { get; set; } = "rodriguezyardclayton@gmail.com";
    /// <summary>If true, DocuSeal emails the seller. Self-hosted needs SMTP in DocuSeal.</summary>
    public bool SendEmail { get; set; }
}
