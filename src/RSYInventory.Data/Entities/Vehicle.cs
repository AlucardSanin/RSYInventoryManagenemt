using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Entities;

/// <summary>
/// Incoming vehicle acquired by a VehicleAcquirer. Location is assigned later
/// by inventory staff; acquisition does not place it in the yard.
/// </summary>
public class Vehicle
{
    public int Id { get; set; }

    public string Vin { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }

    public TransmissionType? TransmissionType { get; set; }
    public VehicleDriveType? DriveType { get; set; }

    public int? Mileage { get; set; }
    public string? Observations { get; set; }

    public int VehicleSourceId { get; set; }
    public VehicleSource VehicleSource { get; set; } = null!;

    public DateTime AcquiredAt { get; set; }
    public int AcquiredByUserId { get; set; }
    public User AcquiredByUser { get; set; } = null!;

    /// <summary>Optional yard location (vehicle zone pallet). Null until assigned.</summary>
    public int? PalletId { get; set; }
    public Pallet? Pallet { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
