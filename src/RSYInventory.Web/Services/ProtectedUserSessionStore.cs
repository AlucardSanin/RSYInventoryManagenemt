using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using RSYInventory.Data.Services;

namespace RSYInventory.Web.Services;

/// <summary>
/// Persists the signed-in user id in protected localStorage so login survives
/// browser restarts (not just the current tab session).
/// </summary>
public sealed class ProtectedUserSessionStore : IUserSessionStore
{
    private const string UserKey = "rsy.auth.userId";
    private const string SkipAutoKey = "rsy.auth.skipAuto";
    private readonly ProtectedLocalStorage _local;
    private readonly ProtectedSessionStorage _session;

    public ProtectedUserSessionStore(
        ProtectedLocalStorage local,
        ProtectedSessionStorage session)
    {
        _local = local;
        _session = session;
    }

    public async Task<int?> GetUserIdAsync()
    {
        var fromLocal = await TryGetIntAsync(_local, UserKey);
        if (fromLocal is not null)
            return fromLocal;

        // Migrate older sessionStorage logins into localStorage once.
        var fromSession = await TryGetIntAsync(_session, UserKey);
        if (fromSession is not null)
        {
            await _local.SetAsync(UserKey, fromSession.Value);
            try { await _session.DeleteAsync(UserKey); } catch { /* ignore */ }
        }

        return fromSession;
    }

    public async Task SetUserIdAsync(int userId)
    {
        await _local.SetAsync(UserKey, userId);
        try { await _session.DeleteAsync(UserKey); } catch { /* ignore */ }
    }

    public async Task ClearAsync()
    {
        try { await _local.DeleteAsync(UserKey); } catch { /* ignore */ }
        try { await _session.DeleteAsync(UserKey); } catch { /* ignore */ }
    }

    public async Task<bool> GetSkipAutoLoginAsync()
    {
        var local = await TryGetBoolAsync(_local, SkipAutoKey);
        if (local is not null)
            return local.Value;

        var session = await TryGetBoolAsync(_session, SkipAutoKey);
        return session == true;
    }

    public async Task SetSkipAutoLoginAsync(bool skip)
    {
        if (skip)
        {
            await _local.SetAsync(SkipAutoKey, true);
        }
        else
        {
            try { await _local.DeleteAsync(SkipAutoKey); } catch { /* ignore */ }
            try { await _session.DeleteAsync(SkipAutoKey); } catch { /* ignore */ }
        }
    }

    private static async Task<int?> TryGetIntAsync(ProtectedBrowserStorage storage, string key)
    {
        try
        {
            var result = await storage.GetAsync<int>(key);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool?> TryGetBoolAsync(ProtectedBrowserStorage storage, string key)
    {
        try
        {
            var result = await storage.GetAsync<bool>(key);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }
}
