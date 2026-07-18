using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Fixed demo user (Id = 1) with elevated permissions for local debugging.
/// Replace with real auth later.
/// </summary>
public sealed class DevCurrentUserService : ICurrentUserService
{
    public int UserId => 1;
    public string DisplayName => "Demo Admin";

    public bool HasRole(AppRole role) => true;

    public bool CanViewInventory => true;
    public bool CanEditInventory => true;
    public bool CanManageZones => true;
    public bool CanAcquireVehicles => true;
}
