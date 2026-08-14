using System;

namespace RSYInventory.Data.Entities;

public partial class RecycleBillToCompany
{
    public int Id { get; set; }

    public string Alias { get; set; } = null!;

    public string CompanyName { get; set; } = null!;

    public string AddressLine { get; set; } = null!;

    public string? ContactLine { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
