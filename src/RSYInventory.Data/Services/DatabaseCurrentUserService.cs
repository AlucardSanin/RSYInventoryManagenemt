using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RSYInventory.Data.Data;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Resolves the current user from SQL Server (Users / UserRoles / Roles).
/// Configure App:CurrentUserName until real authentication is added.
/// </summary>
public sealed class DatabaseCurrentUserService : ICurrentUserService
{
    private readonly YardInventoryDbContext _db;
    private readonly string _userName;
    private bool _loaded;
    private int _userId;
    private string _displayName = string.Empty;
    private HashSet<AppRole> _roles = [];

    public DatabaseCurrentUserService(YardInventoryDbContext db, IConfiguration configuration)
    {
        _db = db;
        _userName = configuration["App:CurrentUserName"] ?? "demo";
    }

    public int UserId
    {
        get
        {
            EnsureLoaded();
            return _userId;
        }
    }

    public string DisplayName
    {
        get
        {
            EnsureLoaded();
            return _displayName;
        }
    }

    public bool HasRole(AppRole role)
    {
        EnsureLoaded();
        return _roles.Contains(role);
    }

    public bool CanViewInventory => HasRole(AppRole.InventoryViewer)
                                    || CanEditInventory
                                    || CanManageZones;

    public bool CanEditInventory => HasRole(AppRole.InventoryEditor) || CanManageZones;

    public bool CanManageZones => HasRole(AppRole.ZoneManager);

    public bool CanAcquireVehicles => HasRole(AppRole.VehicleAcquirer)
                                      || CanEditInventory
                                      || CanManageZones;

    private void EnsureLoaded()
    {
        if (_loaded)
            return;

        var user = _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefault(u => u.UserName == _userName && u.IsActive)
            ?? throw new InvalidOperationException(
                $"Usuario '{_userName}' no encontrado en RSYYardInventory. " +
                "Aplica resources/Database/*.sql en SSMS (p. ej. 002_DemoUserAndPartsZone.sql).");

        _userId = user.Id;
        _displayName = user.DisplayName;
        _roles = user.UserRoles.Select(ur => ur.Role.Code).ToHashSet();
        _loaded = true;
    }
}
