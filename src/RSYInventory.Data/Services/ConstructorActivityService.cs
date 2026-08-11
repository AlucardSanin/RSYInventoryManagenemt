using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class ConstructorActivityService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly AuditService _audit;

    public ConstructorActivityService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        AuditService audit)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<User>> GetConstructorDriversAsync(CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Code == (int)AppRole.ConstructorDriver))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<List<ConstructorActivityLog>> SearchAsync(
        int? driverUserId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var q = db.ConstructorActivityLogs
            .AsNoTracking()
            .Include(a => a.Driver)
            .Include(a => a.RecordedByUser)
            .AsQueryable();

        if (driverUserId is > 0)
            q = q.Where(a => a.DriverUserId == driverUserId);

        if (from is not null)
        {
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), YardTimeZone.EasternInfo);
            q = q.Where(a => a.RecordedAtUtc >= fromUtc);
        }

        if (to is not null)
        {
            var toExclusive = TimeZoneInfo.ConvertTimeToUtc(
                to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                YardTimeZone.EasternInfo);
            q = q.Where(a => a.RecordedAtUtc < toExclusive);
        }

        return await q
            .OrderByDescending(a => a.RecordedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<ConstructorActivityLog> CreateActivityAsync(
        int driverUserId,
        string? notes,
        bool recordedByStaff,
        CancellationToken ct = default)
    {
        if (recordedByStaff)
            EnsureCanManage();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == driverUserId && u.IsActive, ct)
            ?? throw new InvalidOperationException("Chofer de constructora no encontrado.");

        if (!driver.Roles.Any(r => r.Code == (int)AppRole.ConstructorDriver))
            throw new InvalidOperationException("El usuario seleccionado no es chofer de constructora.");

        var actingUserId = recordedByStaff ? _currentUser.UserId : driverUserId;
        var entity = new ConstructorActivityLog
        {
            DriverUserId = driverUserId,
            RelativePath = "",
            RecordedAtUtc = DateTime.UtcNow,
            RecordedByUserId = actingUserId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.ConstructorActivityLogs.Add(entity);
        await db.SaveChangesAsync(ct);

        if (recordedByStaff)
        {
            await _audit.WriteAsync(
                "ConstructorActivityRegistered",
                "ConstructorActivityLog",
                entity.Id,
                $"Actividad constructora · {driver.DisplayName}",
                null,
                ct);
        }
        else
        {
            await _audit.WriteForUserAsync(
                actingUserId,
                "ConstructorActivityRegistered",
                "ConstructorActivityLog",
                entity.Id,
                "Chofer registró actividad",
                null,
                ct);
        }

        return entity;
    }

    public async Task SetImageAsync(
        int activityId,
        string relativePath,
        Guid? driverToken = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Debes subir una foto de actividad.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ConstructorActivityLogs.FirstOrDefaultAsync(a => a.Id == activityId, ct)
            ?? throw new InvalidOperationException("Registro no encontrado.");

        if (driverToken is Guid token)
        {
            var driver = await ResolveConstructorDriverByTokenAsync(db, token, ct)
                ?? throw new InvalidOperationException("Enlace inválido.");
            if (entity.DriverUserId != driver.Id)
                throw new UnauthorizedAccessException("Este registro no pertenece a este chofer.");
        }
        else
        {
            EnsureCanManage();
        }

        entity.RelativePath = relativePath.Trim();
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ConstructorActivityLogs.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new InvalidOperationException("Registro no encontrado.");
        db.ConstructorActivityLogs.Remove(entity);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "ConstructorActivityDeleted",
            "ConstructorActivityLog",
            id,
            "Actividad de constructora eliminada",
            null,
            ct);
    }

    public async Task<(User Driver, List<ConstructorActivityLog> Recent)?> GetDriverPortalAsync(
        Guid token,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveConstructorDriverByTokenAsync(db, token, ct);
        if (driver is null) return null;

        var recent = await db.ConstructorActivityLogs
            .AsNoTracking()
            .Where(a => a.DriverUserId == driver.Id)
            .OrderByDescending(a => a.RecordedAtUtc)
            .Take(30)
            .ToListAsync(ct);

        return (driver, recent);
    }

    public async Task<ConstructorActivityLog> CreateActivityFromDriverAsync(
        Guid token,
        string? notes,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveConstructorDriverByTokenAsync(db, token, ct)
            ?? throw new InvalidOperationException("Enlace inválido.");

        return await CreateActivityAsync(driver.Id, notes, recordedByStaff: false, ct);
    }

    private static async Task<User?> ResolveConstructorDriverByTokenAsync(
        YardInventoryDbContext db,
        Guid token,
        CancellationToken ct)
    {
        var driver = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.DriverAccessToken == token && u.IsActive, ct);

        if (driver is null || !driver.Roles.Any(r => r.Code == (int)AppRole.ConstructorDriver))
            return null;
        return driver;
    }

    private void EnsureCanManage()
    {
        if (!_currentUser.CanManageScrapLogistics)
            throw new UnauthorizedAccessException("No tiene permiso para la agenda de constructora.");
    }
}
