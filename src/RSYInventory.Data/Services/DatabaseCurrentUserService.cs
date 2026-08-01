using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Reads the signed-in user from <see cref="CurrentUserState"/> (filled by <see cref="AuthService"/>).
/// </summary>
public sealed class DatabaseCurrentUserService : ICurrentUserService
{
    private readonly CurrentUserState _state;

    public DatabaseCurrentUserService(CurrentUserState state)
    {
        _state = state;
    }

    public bool IsAuthenticated => _state.IsAuthenticated;

    public int UserId => Require().UserId;

    public string UserName => Require().UserName;

    public string DisplayName => Require().DisplayName;

    public bool HasRole(AppRole role) =>
        _state.Snapshot?.Roles.Contains(role) == true;

    public bool CanViewInventory => HasRole(AppRole.InventoryViewer)
                                    || CanEditInventory
                                    || CanManageZones;

    public bool CanEditInventory => HasRole(AppRole.InventoryEditor) || CanManageZones || CanManageUsers;

    public bool CanManageZones => HasRole(AppRole.ZoneManager) || CanManageUsers;

    public bool CanAcquireVehicles => HasRole(AppRole.VehicleAcquirer)
                                      || CanEditInventory;

    /// <summary>
    /// Inventory viewers (and anyone who can acquire/edit) can open the vehicle list and detail pages.
    /// </summary>
    public bool CanViewVehicles => CanViewInventory || CanAcquireVehicles || CanEditVehicles;

    /// <summary>
    /// Anyone who can acquire vehicles (or god) can edit vehicle details and manage photos.
    /// </summary>
    public bool CanEditVehicles => CanAcquireVehicles || CanManageUsers;

    public bool CanManageUsers => HasRole(AppRole.SystemAdmin);

    private CurrentUserSnapshot Require() =>
        _state.Snapshot
        ?? throw new InvalidOperationException("No hay sesión activa. Inicie sesión.");
}
