namespace RSYInventory.Data.Services;

/// <summary>
/// Persists the signed-in user id across refreshes (implemented in the Web host).
/// </summary>
public interface IUserSessionStore
{
    Task<int?> GetUserIdAsync();
    Task SetUserIdAsync(int userId);
    Task ClearAsync();

    /// <summary>When true, Development auto-login is skipped (user chose Cerrar sesión).</summary>
    Task<bool> GetSkipAutoLoginAsync();
    Task SetSkipAutoLoginAsync(bool skip);
}
