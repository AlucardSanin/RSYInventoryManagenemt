using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Entities;

public class Role
{
    public int Id { get; set; }
    public AppRole Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
