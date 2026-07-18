using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Data;

/// <summary>
/// Idempotent seed for reference/demo rows that must exist in SQL Server
/// (roles, sources, zones, demo user). Safe to run on every startup.
/// Keep in sync with resources/Database/*.sql scripts.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task EnsureSeededAsync(YardInventoryDbContext db, CancellationToken ct = default)
    {
        await EnsureRolesAsync(db, ct);
        await EnsureVehicleSourcesAsync(db, ct);
        await EnsureDemoUserAsync(db, ct);
        await EnsureDefaultZonesAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureRolesAsync(YardInventoryDbContext db, CancellationToken ct)
    {
        if (await db.Roles.AnyAsync(ct))
            return;

        db.Roles.AddRange(
            new Role { Id = 1, Code = AppRole.InventoryViewer, Name = "Inventory Viewer", Description = "View inventory and locations only." },
            new Role { Id = 2, Code = AppRole.InventoryEditor, Name = "Inventory Editor", Description = "Add, edit, remove inventory; assign vehicle locations." },
            new Role { Id = 3, Code = AppRole.ZoneManager, Name = "Zone Manager", Description = "Create and edit zones, rows, and pallets for parts and vehicles." },
            new Role { Id = 4, Code = AppRole.VehicleAcquirer, Name = "Vehicle Acquirer", Description = "Register newly acquired vehicles without assigning yard location." });
    }

    private static async Task EnsureVehicleSourcesAsync(YardInventoryDbContext db, CancellationToken ct)
    {
        if (await db.VehicleSources.AnyAsync(ct))
            return;

        db.VehicleSources.AddRange(
            new VehicleSource { Name = "Wheelzy", IsActive = true },
            new VehicleSource { Name = "Pebble", IsActive = true },
            new VehicleSource { Name = "Facebook", IsActive = true },
            new VehicleSource { Name = "Other", IsActive = true });
    }

    private static async Task EnsureDemoUserAsync(YardInventoryDbContext db, CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserName == "demo", ct);

        if (user is null)
        {
            user = new User
            {
                UserName = "demo",
                DisplayName = "Demo Admin",
                Email = "demo@rsy.local",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
        }

        var roleIds = await db.Roles.Select(r => r.Id).ToListAsync(ct);
        var existing = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        foreach (var roleId in roleIds.Where(id => !existing.Contains(id)))
        {
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }
    }

    private static async Task EnsureDefaultZonesAsync(YardInventoryDbContext db, CancellationToken ct)
    {
        await EnsureZoneWithLayoutAsync(db, "Zona A", ZonePurpose.Vehicles, rowCount: 2, palletsPerRow: 2, labelPrefix: "A", ct);
        await EnsureZoneWithLayoutAsync(db, "Zona B", ZonePurpose.Vehicles, rowCount: 2, palletsPerRow: 2, labelPrefix: "B", ct);
        await EnsureZoneWithLayoutAsync(db, "Zona Partes", ZonePurpose.Parts, rowCount: 2, palletsPerRow: 2, labelPrefix: "P", ct);
    }

    private static async Task EnsureZoneWithLayoutAsync(
        YardInventoryDbContext db,
        string name,
        ZonePurpose purpose,
        int rowCount,
        int palletsPerRow,
        string labelPrefix,
        CancellationToken ct)
    {
        var exists = await db.Zones.AnyAsync(z => z.Name == name && z.Purpose == purpose, ct);
        if (exists)
            return;

        var zone = new Zone
        {
            Name = name,
            Purpose = purpose,
            RowCount = rowCount,
            PalletsPerRow = palletsPerRow,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        for (var r = 1; r <= rowCount; r++)
        {
            var row = new Row
            {
                RowNumber = r,
                Label = $"{labelPrefix}-R{r}",
                IsActive = true
            };

            for (var p = 1; p <= palletsPerRow; p++)
            {
                row.Pallets.Add(new Pallet
                {
                    PalletNumber = p,
                    Label = $"{labelPrefix}-R{r}-P{p}",
                    IsActive = true
                });
            }

            zone.Rows.Add(row);
        }

        db.Zones.Add(zone);
    }
}
