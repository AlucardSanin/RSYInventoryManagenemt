using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class InventoryItem
{
    public int Id { get; set; }

    public int ItemType { get; set; }

    public int Status { get; set; }

    public string? PartNumber { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public int? Year { get; set; }

    public string? Description { get; set; }

    public string? Notes { get; set; }

    /// <summary>Optional donor vehicle VIN used to suggest year/make.</summary>
    public string? SourceVin { get; set; }

    /// <summary>Engine displacement in liters (e.g. 2.50).</summary>
    public decimal? DisplacementLiters { get; set; }

    /// <summary>Transmission style: Automatic / Manual (int enum).</summary>
    public int? TransmissionType { get; set; }

    /// <summary>Drivetrain: FWD / RWD / AWD / 4x4 / 4x2 (int enum).</summary>
    public int? DriveType { get; set; }

    public int? PalletId { get; set; }

    /// <summary>Relative path under wwwroot (e.g. uploads/parts/ford/12/photo.jpg).</summary>
    public string? ImageRelativePath { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public virtual User? CreatedByUser { get; set; }

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual Pallet? Pallet { get; set; }
}
