using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Current signed-in user for the Blazor circuit / request.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    int UserId { get; }
    string UserName { get; }
    string DisplayName { get; }
    bool HasRole(AppRole role);
    bool CanViewInventory { get; }
    bool CanEditInventory { get; }
    bool CanManageZones { get; }
    bool CanAcquireVehicles { get; }
    /// <summary>God user: create/edit users and assign access levels.</summary>
    bool CanManageUsers { get; }
}
