namespace RSYInventory.Data.Entities;

public partial class ScheduledVehiclePickup
{
    public int Id { get; set; }

    public string Vin { get; set; } = null!;

    public int? Year { get; set; }

    public string? Make { get; set; }

    public string? Model { get; set; }

    public int? TransmissionType { get; set; }

    public int? DriveType { get; set; }

    public int? Mileage { get; set; }

    public decimal? PurchasePrice { get; set; }

    public string? Observations { get; set; }

    public int VehicleSourceId { get; set; }

    /// <summary>Calendar date the vehicle should be collected (NC local date).</summary>
    public DateOnly ScheduledPickupDate { get; set; }

    /// <summary>Time window agreed with the seller (e.g. "9 - 12pm").</summary>
    public string? ScheduledPickupWindow { get; set; }

    /// <summary>Street / place where the driver should collect the vehicle.</summary>
    public string? PickupAddress { get; set; }

    public string? SellerName { get; set; }

    public string? SellerPhone { get; set; }

    public string? SellerEmail { get; set; }

    public string? PaymentMethod { get; set; }

    public int? AssignedDriverUserId { get; set; }

    /// <summary><see cref="Enums.ScheduledPickupStatus"/> stored as tinyint.</summary>
    public byte Status { get; set; }

    public DateTime? PickedUpAtUtc { get; set; }

    public int? PromotedVehicleId { get; set; }

    public DateTime? PromotedAtUtc { get; set; }

    public int CreatedByUserId { get; set; }

    public string? ImageRelativePath { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual User? AssignedDriver { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual Vehicle? PromotedVehicle { get; set; }

    public virtual VehicleSource VehicleSource { get; set; } = null!;

    public virtual ICollection<ScheduledVehiclePickupImage> Images { get; set; } = new List<ScheduledVehiclePickupImage>();
}
