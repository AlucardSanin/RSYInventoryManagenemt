using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class Role
{
    public int Id { get; set; }

    public int Code { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
