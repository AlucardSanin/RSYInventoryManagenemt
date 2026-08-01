using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class UserAdminService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly PasswordService _passwords;

    public UserAdminService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        PasswordService passwords)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _passwords = passwords;
    }

    public async Task<List<Role>> GetAssignableRolesAsync(CancellationToken ct = default)
    {
        EnsureGod();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Roles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<List<UserListItem>> GetUsersAsync(CancellationToken ct = default)
    {
        EnsureGod();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var users = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .OrderBy(u => u.UserName)
            .ToListAsync(ct);

        return users.Select(u => new UserListItem(
            u.Id,
            u.UserName,
            u.DisplayName,
            u.Email,
            u.IsActive,
            NormalizeLanguage(u.PreferredLanguage),
            u.DriverAccessToken,
            u.Roles.Select(r => (AppRole)r.Code).OrderBy(c => c).ToList(),
            u.Roles.Select(r => r.Name).OrderBy(n => n).ToList())).ToList();
    }

    public async Task<User> CreateUserAsync(
        string userName,
        string displayName,
        string? email,
        string? password,
        string preferredLanguage,
        IReadOnlyCollection<AppRole> roles,
        CancellationToken ct = default)
    {
        EnsureGod();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        userName = userName.Trim();
        displayName = displayName.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        preferredLanguage = NormalizeLanguage(preferredLanguage);
        var driverOnly = roles.Count == 1 && roles.Contains(AppRole.Driver);

        if (string.IsNullOrWhiteSpace(userName))
            throw new InvalidOperationException("El nombre de usuario es obligatorio.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("El nombre para mostrar es obligatorio.");
        if (roles.Count == 0)
            throw new InvalidOperationException("Seleccione al menos un nivel de acceso.");

        if (!driverOnly)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
                throw new InvalidOperationException("La contraseña debe tener al menos 4 caracteres.");
        }

        if (await db.Users.AnyAsync(u => u.UserName == userName, ct))
            throw new InvalidOperationException("Ya existe un usuario con ese nombre.");

        var roleEntities = await ResolveRolesAsync(db, roles, ct);
        var isDriver = roles.Contains(AppRole.Driver);

        var user = new User
        {
            UserName = userName,
            DisplayName = displayName,
            Email = email,
            PasswordHash = string.IsNullOrWhiteSpace(password) ? null : _passwords.Hash(password),
            IsActive = true,
            PreferredLanguage = preferredLanguage,
            DriverAccessToken = isDriver ? Guid.NewGuid() : null,
            CreatedAtUtc = DateTime.UtcNow,
            Roles = roleEntities
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task UpdateUserAsync(
        int userId,
        string displayName,
        string? email,
        bool isActive,
        string preferredLanguage,
        IReadOnlyCollection<AppRole> roles,
        string? newPassword,
        CancellationToken ct = default)
    {
        EnsureGod();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        displayName = displayName.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        preferredLanguage = NormalizeLanguage(preferredLanguage);

        if (string.IsNullOrWhiteSpace(displayName))
            throw new InvalidOperationException("El nombre para mostrar es obligatorio.");
        if (roles.Count == 0)
            throw new InvalidOperationException("Seleccione al menos un nivel de acceso.");

        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (user.UserName.Equals("demo", StringComparison.OrdinalIgnoreCase)
            && (!isActive || !roles.Contains(AppRole.SystemAdmin)))
        {
            throw new InvalidOperationException(
                "El usuario god (demo) debe permanecer activo y con rol System Admin.");
        }

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            if (newPassword.Length < 4)
                throw new InvalidOperationException("La contraseña debe tener al menos 4 caracteres.");
            user.PasswordHash = _passwords.Hash(newPassword);
        }

        user.DisplayName = displayName;
        user.Email = email;
        user.IsActive = isActive;
        user.PreferredLanguage = preferredLanguage;
        user.Roles = await ResolveRolesAsync(db, roles, ct);

        if (roles.Contains(AppRole.Driver))
            user.DriverAccessToken ??= Guid.NewGuid();
        else
            user.DriverAccessToken = null;

        await db.SaveChangesAsync(ct);
    }

    public async Task<Guid> RotateDriverAccessTokenAsync(int userId, CancellationToken ct = default)
    {
        EnsureGod();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        if (!user.Roles.Any(r => r.Code == (int)AppRole.Driver))
            throw new InvalidOperationException("Solo los choferes tienen enlace de agenda.");

        user.DriverAccessToken = Guid.NewGuid();
        await db.SaveChangesAsync(ct);
        return user.DriverAccessToken.Value;
    }

    private static string NormalizeLanguage(string? language) =>
        string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";

    private static async Task<List<Role>> ResolveRolesAsync(
        YardInventoryDbContext db,
        IReadOnlyCollection<AppRole> roles,
        CancellationToken ct)
    {
        var codes = roles.Select(r => (int)r).Distinct().ToList();
        var entities = await db.Roles.Where(r => codes.Contains(r.Code)).ToListAsync(ct);
        if (entities.Count != codes.Count)
            throw new InvalidOperationException(
                "Uno o más roles no existen en la base de datos. Aplica el script SQL 015_DriverPickupSchedule.sql.");
        return entities;
    }

    private void EnsureGod()
    {
        if (!_currentUser.CanManageUsers)
            throw new UnauthorizedAccessException("Solo el usuario god puede gestionar usuarios.");
    }
}

public sealed record UserListItem(
    int Id,
    string UserName,
    string DisplayName,
    string? Email,
    bool IsActive,
    string PreferredLanguage,
    Guid? DriverAccessToken,
    IReadOnlyList<AppRole> Roles,
    IReadOnlyList<string> RoleNames);
