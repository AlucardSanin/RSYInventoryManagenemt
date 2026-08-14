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

    /// <summary>View acquired vehicle list and details (read-only for inventory viewers).</summary>
    bool CanViewVehicles { get; }

    /// <summary>Edit vehicle details, photos, and purchase receipts (acquirer or admin).</summary>
    bool CanEditVehicles { get; }

    /// <summary>God user: create/edit users and assign access levels.</summary>
    bool CanManageUsers { get; }

    /// <summary>Create/manage scheduled vehicle pickups and driver reports.</summary>
    bool CanManagePickupSchedule { get; }

    /// <summary>Manage recycle loads and constructor activity logs.</summary>
    bool CanManageScrapLogistics { get; }
}
