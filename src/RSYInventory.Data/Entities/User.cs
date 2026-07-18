namespace RSYInventory.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Vehicle> AcquiredVehicles { get; set; } = new List<Vehicle>();
    public ICollection<InventoryMovement> Movements { get; set; } = new List<InventoryMovement>();
}
