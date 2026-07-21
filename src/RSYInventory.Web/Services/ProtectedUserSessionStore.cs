using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RSYInventory.Data.Services;

namespace RSYInventory.Web.Services;

/// <summary>
/// Browser session storage for the signed-in user id.
/// Failures are swallowed by AuthService so the UI never blocks.
/// </summary>
public sealed class ProtectedUserSessionStore : IUserSessionStore
{
    private const string UserKey = "rsy.auth.userId";
    private const string SkipAutoKey = "rsy.auth.skipAuto";
    private readonly ProtectedSessionStorage _storage;

    public ProtectedUserSessionStore(ProtectedSessionStorage storage)
    {
        _storage = storage;
    }

    public async Task<int?> GetUserIdAsync()
    {
        var result = await _storage.GetAsync<int>(UserKey);
        return result.Success ? result.Value : null;
    }

    public async Task SetUserIdAsync(int userId) => await _storage.SetAsync(UserKey, userId);

    public async Task ClearAsync() => await _storage.DeleteAsync(UserKey);

    public async Task<bool> GetSkipAutoLoginAsync()
    {
        var result = await _storage.GetAsync<bool>(SkipAutoKey);
        return result.Success && result.Value;
    }

    public async Task SetSkipAutoLoginAsync(bool skip)
    {
        if (skip)
            await _storage.SetAsync(SkipAutoKey, true);
        else
            await _storage.DeleteAsync(SkipAutoKey);
    }
}
