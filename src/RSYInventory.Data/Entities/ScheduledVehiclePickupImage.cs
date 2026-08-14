namespace RSYInventory.Data.Entities;

public partial class ScheduledVehiclePickupImage
{
    public int Id { get; set; }

    public int ScheduledId { get; set; }

    public string RelativePath { get; set; } = null!;

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual ScheduledVehiclePickup Scheduled { get; set; } = null!;
}
