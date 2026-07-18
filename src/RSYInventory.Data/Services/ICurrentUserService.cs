using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Temporary identity until full authentication is wired.
/// In Development the demo user has all roles for easier Visual Studio debugging.
/// </summary>
public interface ICurrentUserService
{
    int UserId { get; }
    string DisplayName { get; }
    bool HasRole(AppRole role);
    bool CanViewInventory { get; }
    bool CanEditInventory { get; }
    bool CanManageZones { get; }
    bool CanAcquireVehicles { get; }
}
