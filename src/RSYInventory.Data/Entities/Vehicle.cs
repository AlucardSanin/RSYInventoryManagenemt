using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class Vehicle
{
    public int Id { get; set; }

    public string Vin { get; set; } = null!;

    public int? Year { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public int? TransmissionType { get; set; }

    public int? DriveType { get; set; }

    /// <summary>Odometer reading in miles (not kilometers).</summary>
    public int? Mileage { get; set; }

    /// <summary>Optional acquisition purchase price.</summary>
    public decimal? PurchasePrice { get; set; }

    public string? Observations { get; set; }

    public int VehicleSourceId { get; set; }

    public DateTime AcquiredAt { get; set; }

    public int AcquiredByUserId { get; set; }

    public int? PalletId { get; set; }

    /// <summary>Relative path under wwwroot (e.g. uploads/vehicles/ford/12/photo.jpg).</summary>
    public string? ImageRelativePath { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? DeletedAtUtc { get; set; }

    public virtual User AcquiredByUser { get; set; } = null!;

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual Pallet? Pallet { get; set; }

    public virtual VehicleSource VehicleSource { get; set; } = null!;
}
