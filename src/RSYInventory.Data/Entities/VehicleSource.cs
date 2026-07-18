namespace RSYInventory.Data.Entities;

/// <summary>
/// Acquisition channel (Wheelzy, Pebble, Facebook, etc.).
/// </summary>
public class VehicleSource
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
