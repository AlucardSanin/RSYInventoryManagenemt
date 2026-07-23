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
