namespace RSYInventory.Data.Entities;

/// <summary>
/// Audit row for purchase-price changes on acquired vehicles or scheduled pickups.
/// Exactly one of <see cref="VehicleId"/> or <see cref="ScheduledVehiclePickupId"/> is set.
/// </summary>
public partial class VehiclePriceHistory
{
    public long Id { get; set; }

    public int? VehicleId { get; set; }

    public int? ScheduledVehiclePickupId { get; set; }

    public decimal? OldPrice { get; set; }

    public decimal? NewPrice { get; set; }

    public int ChangedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime ChangedAtUtc { get; set; }

    public virtual User ChangedByUser { get; set; } = null!;

    public virtual Vehicle? Vehicle { get; set; }

    public virtual ScheduledVehiclePickup? ScheduledVehiclePickup { get; set; }
}
