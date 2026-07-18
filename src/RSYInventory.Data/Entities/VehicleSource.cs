using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class VehicleSource
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
