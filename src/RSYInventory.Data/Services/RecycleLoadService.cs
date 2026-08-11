using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class RecycleLoadService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly AuditService _audit;

    public RecycleLoadService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        AuditService audit)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<User>> GetRecycleDriversAsync(CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => r.Code == (int)AppRole.RecycleDriver))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<List<RecycleLoad>> SearchAsync(
        int? driverUserId,
        DateOnly? from,
        DateOnly? to,
        string? loadId,
        bool? verifiedOnly,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var q = db.RecycleLoads
            .AsNoTracking()
            .Include(l => l.Driver)
            .Include(l => l.RecordedByUser)
            .Include(l => l.VerifiedByUser)
            .Include(l => l.Documents)
            .AsQueryable();

        if (driverUserId is > 0)
            q = q.Where(l => l.DriverUserId == driverUserId);

        if (from is not null)
        {
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), YardTimeZone.EasternInfo);
            q = q.Where(l => l.RecordedAtUtc >= fromUtc);
        }

        if (to is not null)
        {
            var toExclusive = TimeZoneInfo.ConvertTimeToUtc(
                to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue),
                YardTimeZone.EasternInfo);
            q = q.Where(l => l.RecordedAtUtc < toExclusive);
        }

        if (!string.IsNullOrWhiteSpace(loadId))
        {
            var needle = loadId.Trim();
            q = q.Where(l => EF.Functions.Like(l.LoadExternalId, $"%{needle}%"));
        }

        if (verifiedOnly is true)
            q = q.Where(l => l.IsVerified);
        else if (verifiedOnly is false)
            q = q.Where(l => !l.IsVerified);

        return await q
            .OrderByDescending(l => l.RecordedAtUtc)
            .ThenByDescending(l => l.Id)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<RecycleLoad?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.RecycleLoads
            .AsNoTracking()
            .Include(l => l.Driver)
            .Include(l => l.RecordedByUser)
            .Include(l => l.VerifiedByUser)
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
    }

    public async Task<RecycleLoad> CreateLoadAsync(
        string loadExternalId,
        int driverUserId,
        string? notes,
        bool recordedByStaff,
        CancellationToken ct = default)
    {
        if (recordedByStaff)
            EnsureCanManage();

        loadExternalId = (loadExternalId ?? string.Empty).Trim();
        if (loadExternalId.Length == 0)
            throw new InvalidOperationException("El ID de la carga es obligatorio.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == driverUserId && u.IsActive, ct)
            ?? throw new InvalidOperationException("Chofer de reciclaje no encontrado.");

        if (!driver.Roles.Any(r => r.Code == (int)AppRole.RecycleDriver))
            throw new InvalidOperationException("El usuario seleccionado no es chofer de reciclaje.");

        var actingUserId = recordedByStaff ? _currentUser.UserId : driverUserId;

        var entity = new RecycleLoad
        {
            LoadExternalId = loadExternalId,
            DriverUserId = driverUserId,
            RecordedAtUtc = DateTime.UtcNow,
            RecordedByUserId = actingUserId,
            IsVerified = false,
            VerifiedAtUtc = null,
            VerifiedByUserId = null,
            Notes = NullIfWhiteSpace(notes),
            CreatedAtUtc = DateTime.UtcNow
        };

        db.RecycleLoads.Add(entity);
        await db.SaveChangesAsync(ct);

        if (recordedByStaff)
        {
            await _audit.WriteAsync(
                "RecycleLoadRegistered",
                "RecycleLoad",
                entity.Id,
                $"Carga reciclaje {entity.LoadExternalId} · chofer {driver.DisplayName}",
                "Registrada por staff",
                ct);
        }
        else
        {
            await _audit.WriteForUserAsync(
                actingUserId,
                "RecycleLoadRegistered",
                "RecycleLoad",
                entity.Id,
                $"Chofer registró carga {entity.LoadExternalId}",
                "Pendiente de verificación",
                ct);
        }

        return entity;
    }

    public async Task AttachDocumentAsync(
        int loadId,
        RecycleDocumentType type,
        string relativePath,
        Guid? driverToken = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Ruta de documento inválida.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var load = await db.RecycleLoads
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == loadId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");

        if (driverToken is Guid token)
        {
            var driver = await ResolveRecycleDriverByTokenAsync(db, token, ct)
                ?? throw new InvalidOperationException("Enlace inválido.");
            if (load.DriverUserId != driver.Id)
                throw new UnauthorizedAccessException("Esta carga no pertenece a este chofer.");
        }
        else
        {
            EnsureCanManage();
        }

        var existing = load.Documents.FirstOrDefault(d => d.DocumentType == (byte)type);
        if (existing is null)
        {
            db.RecycleLoadDocuments.Add(new RecycleLoadDocument
            {
                RecycleLoadId = loadId,
                DocumentType = (byte)type,
                RelativePath = relativePath.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.RelativePath = relativePath.Trim();
        }

        load.UpdatedAtUtc = DateTime.UtcNow;
        if (driverToken is not null)
        {
            load.IsVerified = false;
            load.VerifiedAtUtc = null;
            load.VerifiedByUserId = null;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task EnsureCompleteDocumentsAsync(int loadId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var count = await db.RecycleLoadDocuments.CountAsync(d => d.RecycleLoadId == loadId, ct);
        if (count < 3)
            throw new InvalidOperationException("La carga debe tener los 3 documentos: BOL, NUCOR y SCALE.");
    }

    public async Task ReplaceDocumentAsync(
        int loadId,
        RecycleDocumentType type,
        string relativePath,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        await AttachDocumentAsync(loadId, type, relativePath, driverToken: null, ct);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var load = await db.RecycleLoads.FirstAsync(l => l.Id == loadId, ct);
        load.IsVerified = false;
        load.VerifiedAtUtc = null;
        load.VerifiedByUserId = null;
        load.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "RecycleLoadDocumentReplaced",
            "RecycleLoad",
            loadId,
            $"Documento {type} reemplazado · carga {load.LoadExternalId}",
            null,
            ct);
    }

    public async Task UpdateLoadMetaAsync(
        int loadId,
        string loadExternalId,
        int driverUserId,
        string? notes,
        CancellationToken ct = default)
    {
        EnsureCanManage();
        loadExternalId = (loadExternalId ?? string.Empty).Trim();
        if (loadExternalId.Length == 0)
            throw new InvalidOperationException("El ID de la carga es obligatorio.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var load = await db.RecycleLoads.FirstOrDefaultAsync(l => l.Id == loadId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");

        var driverOk = await db.Users.AnyAsync(
            u => u.Id == driverUserId && u.IsActive && u.Roles.Any(r => r.Code == (int)AppRole.RecycleDriver),
            ct);
        if (!driverOk)
            throw new InvalidOperationException("Chofer de reciclaje inválido.");

        load.LoadExternalId = loadExternalId;
        load.DriverUserId = driverUserId;
        load.Notes = NullIfWhiteSpace(notes);
        load.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetVerifiedAsync(int loadId, bool verified, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var load = await db.RecycleLoads.FirstOrDefaultAsync(l => l.Id == loadId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");

        if (verified)
        {
            load.IsVerified = true;
            load.VerifiedAtUtc = DateTime.UtcNow;
            load.VerifiedByUserId = _currentUser.UserId;
        }
        else
        {
            load.IsVerified = false;
            load.VerifiedAtUtc = null;
            load.VerifiedByUserId = null;
        }

        load.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            verified ? "RecycleLoadVerified" : "RecycleLoadUnverified",
            "RecycleLoad",
            loadId,
            verified ? $"Carga {load.LoadExternalId} verificada" : $"Carga {load.LoadExternalId} marcada sin verificar",
            null,
            ct);
    }

    public async Task DeleteAsync(int loadId, CancellationToken ct = default)
    {
        EnsureCanManage();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var load = await db.RecycleLoads
            .Include(l => l.Documents)
            .FirstOrDefaultAsync(l => l.Id == loadId, ct)
            ?? throw new InvalidOperationException("Carga no encontrada.");

        var externalId = load.LoadExternalId;
        db.RecycleLoads.Remove(load);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "RecycleLoadDeleted",
            "RecycleLoad",
            loadId,
            $"Carga eliminada {externalId}",
            null,
            ct);
    }

    public async Task<(User Driver, List<RecycleLoad> Recent)?> GetDriverPortalAsync(
        Guid token,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveRecycleDriverByTokenAsync(db, token, ct);
        if (driver is null) return null;

        var recent = await db.RecycleLoads
            .AsNoTracking()
            .Include(l => l.Documents)
            .Where(l => l.DriverUserId == driver.Id)
            .OrderByDescending(l => l.RecordedAtUtc)
            .Take(30)
            .ToListAsync(ct);

        return (driver, recent);
    }

    public async Task<RecycleLoad> CreateLoadFromDriverAsync(
        Guid token,
        string loadExternalId,
        string? notes,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveRecycleDriverByTokenAsync(db, token, ct)
            ?? throw new InvalidOperationException("Enlace inválido.");

        return await CreateLoadAsync(loadExternalId, driver.Id, notes, recordedByStaff: false, ct);
    }

    private static async Task<User?> ResolveRecycleDriverByTokenAsync(
        YardInventoryDbContext db,
        Guid token,
        CancellationToken ct)
    {
        var driver = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.DriverAccessToken == token && u.IsActive, ct);

        if (driver is null || !driver.Roles.Any(r => r.Code == (int)AppRole.RecycleDriver))
            return null;
        return driver;
    }

    private void EnsureCanManage()
    {
        if (!_currentUser.CanManageScrapLogistics)
            throw new UnauthorizedAccessException("No tiene permiso para la agenda de reciclaje.");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
