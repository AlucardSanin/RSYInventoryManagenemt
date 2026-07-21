namespace RSYInventory.Data.Entities;

public partial class VehicleImage
{
    public int Id { get; set; }

    public int VehicleId { get; set; }

    /// <summary>Relative path under wwwroot (e.g. uploads/vehicles/ford/12/photo-….jpg).</summary>
    public string RelativePath { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual Vehicle Vehicle { get; set; } = null!;
}
