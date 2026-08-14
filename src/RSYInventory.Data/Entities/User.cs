using System;
using System.Collections.Generic;

namespace RSYInventory.Data.Entities;

public partial class User
{
    public int Id { get; set; }

    public string UserName { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public bool IsActive { get; set; }

    /// <summary>UI language for driver portal: "es" or "en".</summary>
    public string PreferredLanguage { get; set; } = "es";

    /// <summary>Passwordless access token for the driver pickup list URL.</summary>
    public Guid? DriverAccessToken { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual ICollection<AuditEvent> AuditEvents { get; set; } = new List<AuditEvent>();

    public virtual ICollection<InventoryItem> CreatedInventoryItems { get; set; } = new List<InventoryItem>();

    public virtual ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();

    public virtual ICollection<Vehicle> PickedUpVehicles { get; set; } = new List<Vehicle>();

    public virtual ICollection<ScheduledVehiclePickup> AssignedPickups { get; set; } = new List<ScheduledVehiclePickup>();

    public virtual ICollection<ScheduledVehiclePickup> CreatedPickups { get; set; } = new List<ScheduledVehiclePickup>();

    public virtual ICollection<RecycleLoad> RecycleLoadsAsDriver { get; set; } = new List<RecycleLoad>();

    public virtual ICollection<RecycleLoad> RecycleLoadsRecorded { get; set; } = new List<RecycleLoad>();

    public virtual ICollection<ConstructorActivityLog> ConstructorActivitiesAsDriver { get; set; } = new List<ConstructorActivityLog>();

    public virtual ICollection<ConstructorActivityLog> ConstructorActivitiesRecorded { get; set; } = new List<ConstructorActivityLog>();

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}
