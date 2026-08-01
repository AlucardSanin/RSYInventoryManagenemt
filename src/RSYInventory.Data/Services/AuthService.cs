using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class AuthService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly IUserSessionStore _session;
    private readonly PasswordService _passwords;
    private readonly CurrentUserState _state;
    private readonly IHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AuthService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        IUserSessionStore session,
        PasswordService passwords,
        CurrentUserState state,
        IHostEnvironment env,
        IConfiguration config)
    {
        _dbFactory = dbFactory;
        _session = session;
        _passwords = passwords;
        _state = state;
        _env = env;
        _config = config;
    }

    /// <summary>
    /// Restores session from browser storage, or auto-logs demo in Development.
    /// Always marks the auth state as resolved so the UI never stays on "Cargando…".
    /// </summary>
    public async Task RestoreAsync()
    {
        if (_state.Resolved)
            return;

        await _gate.WaitAsync();
        try
        {
            if (_state.Resolved)
                return;

            try
            {
                var userId = await _session.GetUserIdAsync();
                if (userId is not null)
                {
                    var loaded = await LoadUserAsync(userId.Value);
                    if (loaded is not null)
                    {
                        _state.Set(loaded);
                        return;
                    }

                    await SafeClearSessionAsync();
                }

                var skipAuto = await SafeGetSkipAutoAsync();
                if (!skipAuto && _env.IsDevelopment())
                {
                    var autoUser = _config["App:CurrentUserName"] ?? "demo";
                    if (await TrySignInByUserNameAsync(autoUser))
                        return;
                }
            }
            catch
            {
                // JS interop / DB unavailable — fall through to anonymous.
            }

            _state.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LoginAsync(string userName, string password)
    {
        userName = userName.Trim();
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Usuario y contraseña son obligatorios.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive)
            ?? throw new InvalidOperationException("Usuario o contraseña incorrectos.");

        if (string.IsNullOrEmpty(user.PasswordHash))
            throw new InvalidOperationException(
                "Este usuario no tiene contraseña. Aplica resources/Database/003_UserAuthAndSystemAdmin.sql.");

        if (!_passwords.Verify(user, password))
            throw new InvalidOperationException("Usuario o contraseña incorrectos.");

        await SafeSetSkipAutoAsync(false);
        await SafeSetSessionAsync(user.Id);
        _state.Set(ToSnapshot(user));
    }

    public async Task LogoutAsync()
    {
        await SafeClearSessionAsync();
        await SafeSetSkipAutoAsync(true);
        _state.Clear();
    }

    /// <summary>
    /// Verifies that <paramref name="password"/> belongs to an active SystemAdmin.
    /// Prefers the current user when they are admin; otherwise accepts any active admin password.
    /// </summary>
    public async Task VerifySystemAdminPasswordAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password))
            throw new InvalidOperationException("La contraseña de administrador es obligatoria.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var adminCode = (int)AppRole.SystemAdmin;
        var admins = await db.Users
            .Include(u => u.Roles)
            .Where(u => u.IsActive && u.Roles.Any(r => r.Code == adminCode))
            .ToListAsync(ct);

        if (admins.Count == 0)
            throw new InvalidOperationException("No hay usuarios administradores activos en el sistema.");

        if (_state.Snapshot is { } current && current.Roles.Contains(AppRole.SystemAdmin))
        {
            var me = admins.FirstOrDefault(u => u.Id == current.UserId);
            if (me is not null && _passwords.Verify(me, password))
                return;
        }

        if (admins.Any(u => _passwords.Verify(u, password)))
            return;

        throw new InvalidOperationException("Contraseña de administrador incorrecta.");
    }

    private async Task<bool> TrySignInByUserNameAsync(string userName)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);

        if (user is null)
            return false;

        await SafeSetSessionAsync(user.Id);
        _state.Set(ToSnapshot(user));
        return true;
    }

    private async Task<CurrentUserSnapshot?> LoadUserAsync(int userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        return user is null ? null : ToSnapshot(user);
    }

    private async Task SafeSetSessionAsync(int userId)
    {
        try { await _session.SetUserIdAsync(userId); }
        catch { /* optional */ }
    }

    private async Task SafeClearSessionAsync()
    {
        try { await _session.ClearAsync(); }
        catch { /* optional */ }
    }

    private async Task SafeSetSkipAutoAsync(bool skip)
    {
        try { await _session.SetSkipAutoLoginAsync(skip); }
        catch { /* optional */ }
    }

    private async Task<bool> SafeGetSkipAutoAsync()
    {
        try { return await _session.GetSkipAutoLoginAsync(); }
        catch { return false; }
    }

    private static CurrentUserSnapshot ToSnapshot(User user) => new(
        user.Id,
        user.UserName,
        user.DisplayName,
        user.Roles.Select(r => (AppRole)r.Code).ToHashSet());
}
