using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class PickupScheduleService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;
    private readonly AuditService _audit;

    public PickupScheduleService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        AuditService audit)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<List<User>> GetDriverUsersAsync(CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => u.IsActive && u.Roles.Any(r => r.Code == (int)AppRole.Driver))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<List<ScheduledVehiclePickup>> GetActiveScheduleAsync(CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.AssignedDriver)
            .Include(s => s.Images)
            .Where(s => s.Status != (byte)ScheduledPickupStatus.Promoted)
            .OrderBy(s => s.ScheduledPickupDate)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);
    }

    public async Task<ScheduledVehiclePickup?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.AssignedDriver)
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<ScheduledVehiclePickup> ScheduleAsync(
        string vin,
        int? year,
        string? make,
        string? model,
        TransmissionType? transmissionType,
        VehicleDriveType? driveType,
        int? mileage,
        decimal? purchasePrice,
        string? observations,
        int vehicleSourceId,
        DateOnly scheduledPickupDate,
        string? scheduledPickupWindow,
        string? pickupAddress,
        string? sellerName,
        string? sellerPhone,
        string? sellerEmail,
        string? paymentMethod,
        int? assignedDriverUserId,
        CancellationToken ct = default)
    {
        EnsureCanManageSchedule();

        vin = vin.Trim().ToUpperInvariant();
        if (vin.Length is < 11 or > 17)
            throw new InvalidOperationException("El VIN debe tener entre 11 y 17 caracteres.");
        if (vehicleSourceId <= 0)
            throw new InvalidOperationException("Selecciona la fuente.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.Vehicles.AnyAsync(v => v.Vin == vin, ct))
            throw new InvalidOperationException("Ese VIN ya está en vehículos adquiridos.");

        if (await db.ScheduledVehiclePickups.AnyAsync(
                s => s.Vin == vin && s.Status != (byte)ScheduledPickupStatus.Promoted, ct))
            throw new InvalidOperationException("Ese VIN ya está en la agenda de recolección.");

        if (!await db.VehicleSources.AnyAsync(s => s.Id == vehicleSourceId && s.IsActive, ct))
            throw new InvalidOperationException("Fuente inválida.");

        int? driverId = assignedDriverUserId is > 0 ? assignedDriverUserId : null;
        if (driverId is not null)
        {
            var driverOk = await db.Users
                .Include(u => u.Roles)
                .AnyAsync(u => u.Id == driverId
                               && u.IsActive
                               && u.Roles.Any(r => r.Code == (int)AppRole.Driver), ct);
            if (!driverOk)
                throw new InvalidOperationException("El chofer seleccionado no es válido.");
        }

        var entity = new ScheduledVehiclePickup
        {
            Vin = vin,
            Year = year,
            Make = NullIfWhiteSpace(make),
            Model = NullIfWhiteSpace(model),
            TransmissionType = transmissionType is null ? null : (int)transmissionType,
            DriveType = driveType is null ? null : (int)driveType,
            Mileage = mileage,
            PurchasePrice = purchasePrice is null or <= 0
                ? null
                : Math.Round(purchasePrice.Value, 2, MidpointRounding.AwayFromZero),
            Observations = NullIfWhiteSpace(observations),
            VehicleSourceId = vehicleSourceId,
            ScheduledPickupDate = scheduledPickupDate,
            ScheduledPickupWindow = NullIfWhiteSpace(scheduledPickupWindow),
            PickupAddress = NullIfWhiteSpace(pickupAddress),
            SellerName = NullIfWhiteSpace(sellerName),
            SellerPhone = NullIfWhiteSpace(sellerPhone),
            SellerEmail = NullIfWhiteSpace(sellerEmail),
            PaymentMethod = NullIfWhiteSpace(paymentMethod),
            AssignedDriverUserId = driverId,
            Status = (byte)ScheduledPickupStatus.Pending,
            CreatedByUserId = _currentUser.UserId,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.ScheduledVehiclePickups.Add(entity);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "PickupScheduled",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Agenda creada: {entity.Vin}",
            $"Fecha {entity.ScheduledPickupDate:yyyy-MM-dd}"
            + (string.IsNullOrWhiteSpace(entity.ScheduledPickupWindow) ? "" : $" · {entity.ScheduledPickupWindow}")
            + (driverId is null ? " · sin chofer" : $" · chofer #{driverId}"),
            ct);

        return entity;
    }

    public async Task UpdatePendingAsync(
        int id,
        string vin,
        int? year,
        string? make,
        string? model,
        TransmissionType? transmissionType,
        VehicleDriveType? driveType,
        int? mileage,
        decimal? purchasePrice,
        string? observations,
        int vehicleSourceId,
        DateOnly scheduledPickupDate,
        string? scheduledPickupWindow,
        string? pickupAddress,
        string? sellerName,
        string? sellerPhone,
        string? sellerEmail,
        string? paymentMethod,
        int? assignedDriverUserId,
        CancellationToken ct = default)
    {
        EnsureCanManageSchedule();

        vin = vin.Trim().ToUpperInvariant();
        if (vin.Length is < 11 or > 17)
            throw new InvalidOperationException("El VIN debe tener entre 11 y 17 caracteres.");
        if (vehicleSourceId <= 0)
            throw new InvalidOperationException("Selecciona la fuente.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledVehiclePickups
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new InvalidOperationException("Agenda no encontrada.");

        if (entity.Status != (byte)ScheduledPickupStatus.Pending)
            throw new InvalidOperationException("Solo se pueden editar recolecciones pendientes.");

        if (await db.Vehicles.AnyAsync(v => v.Vin == vin, ct))
            throw new InvalidOperationException("Ese VIN ya está en vehículos adquiridos.");

        if (await db.ScheduledVehiclePickups.AnyAsync(
                s => s.Id != id
                     && s.Vin == vin
                     && s.Status != (byte)ScheduledPickupStatus.Promoted, ct))
            throw new InvalidOperationException("Ese VIN ya está en la agenda de recolección.");

        if (!await db.VehicleSources.AnyAsync(s => s.Id == vehicleSourceId && s.IsActive, ct))
            throw new InvalidOperationException("Fuente inválida.");

        int? driverId = assignedDriverUserId is > 0 ? assignedDriverUserId : null;
        if (driverId is not null)
        {
            var driverOk = await db.Users
                .Include(u => u.Roles)
                .AnyAsync(u => u.Id == driverId
                               && u.IsActive
                               && u.Roles.Any(r => r.Code == (int)AppRole.Driver), ct);
            if (!driverOk)
                throw new InvalidOperationException("El chofer seleccionado no es válido.");
        }

        var previousVin = entity.Vin;
        entity.Vin = vin;
        entity.Year = year;
        entity.Make = NullIfWhiteSpace(make);
        entity.Model = NullIfWhiteSpace(model);
        entity.TransmissionType = transmissionType is null ? null : (int)transmissionType;
        entity.DriveType = driveType is null ? null : (int)driveType;
        entity.Mileage = mileage;
        entity.PurchasePrice = purchasePrice is null or <= 0
            ? null
            : Math.Round(purchasePrice.Value, 2, MidpointRounding.AwayFromZero);
        entity.Observations = NullIfWhiteSpace(observations);
        entity.VehicleSourceId = vehicleSourceId;
        entity.ScheduledPickupDate = scheduledPickupDate;
        entity.ScheduledPickupWindow = NullIfWhiteSpace(scheduledPickupWindow);
        entity.PickupAddress = NullIfWhiteSpace(pickupAddress);
        entity.SellerName = NullIfWhiteSpace(sellerName);
        entity.SellerPhone = NullIfWhiteSpace(sellerPhone);
        entity.SellerEmail = NullIfWhiteSpace(sellerEmail);
        entity.PaymentMethod = NullIfWhiteSpace(paymentMethod);
        entity.AssignedDriverUserId = driverId;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "PickupScheduleUpdated",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Agenda editada: {entity.Vin}",
            previousVin == entity.Vin
                ? $"Fecha {entity.ScheduledPickupDate:yyyy-MM-dd}"
                  + (string.IsNullOrWhiteSpace(entity.ScheduledPickupWindow) ? "" : $" · {entity.ScheduledPickupWindow}")
                : $"VIN {previousVin} → {entity.Vin}; fecha {entity.ScheduledPickupDate:yyyy-MM-dd}",
            ct);
    }

    public async Task DeletePendingAsync(int id, CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledVehiclePickups
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new InvalidOperationException("Agenda no encontrada.");

        if (entity.Status != (byte)ScheduledPickupStatus.Pending)
            throw new InvalidOperationException("Solo se pueden eliminar recolecciones pendientes.");

        var vin = entity.Vin;
        var date = entity.ScheduledPickupDate;
        db.ScheduledVehiclePickups.Remove(entity);
        await db.SaveChangesAsync(ct);

        await _audit.WriteAsync(
            "PickupScheduleDeleted",
            "ScheduledVehiclePickup",
            id,
            $"Agenda eliminada: {vin}",
            $"Fecha programada {date:yyyy-MM-dd}",
            ct);
    }

    public async Task AddImageAsync(int scheduledId, string relativePath, CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledVehiclePickups
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == scheduledId, ct)
            ?? throw new InvalidOperationException("Agenda no encontrada.");

        if (entity.Status == (byte)ScheduledPickupStatus.Promoted)
            throw new InvalidOperationException("Ya fue promovido a adquiridos.");

        var sort = entity.Images.Count == 0 ? 0 : entity.Images.Max(i => i.SortOrder) + 1;
        entity.Images.Add(new ScheduledVehiclePickupImage
        {
            RelativePath = relativePath,
            SortOrder = sort,
            CreatedAtUtc = DateTime.UtcNow
        });
        entity.ImageRelativePath ??= relativePath;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DriverPortalSnapshot?> GetDriverPortalAsync(Guid token, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await db.Users
            .AsNoTracking()
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.DriverAccessToken == token && u.IsActive, ct);

        if (driver is null || !driver.Roles.Any(r => r.Code == (int)AppRole.Driver))
            return null;

        // Drivers only see today's agenda (NC local date) — not future scheduled days.
        var today = YardTimeZone.TodayEastern();
        var active = await db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.Images)
            .Where(s => s.AssignedDriverUserId == driver.Id
                        && s.Status != (byte)ScheduledPickupStatus.Promoted
                        && s.ScheduledPickupDate == today)
            .OrderBy(s => s.ScheduledPickupWindow)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        return new DriverPortalSnapshot(driver, active);
    }

    public async Task<List<ScheduledVehiclePickup>> GetDriverHistoryAsync(
        Guid token,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveDriverByTokenAsync(db, token, ct);

        var q = db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.Images)
            .Where(s => s.AssignedDriverUserId == driver.Id
                        && s.Status != (byte)ScheduledPickupStatus.Pending);

        if (from is not null)
            q = q.Where(s => s.ScheduledPickupDate >= from);
        if (to is not null)
            q = q.Where(s => s.ScheduledPickupDate <= to);

        return await q
            .OrderByDescending(s => s.PickedUpAtUtc ?? s.PromotedAtUtc)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);
    }

    public async Task MarkPickedUpAsync(Guid token, int scheduledId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveDriverByTokenAsync(db, token, ct);
        var entity = await db.ScheduledVehiclePickups
            .FirstOrDefaultAsync(s => s.Id == scheduledId && s.AssignedDriverUserId == driver.Id, ct)
            ?? throw new InvalidOperationException("Pickup not found.");

        EnsureSameDayEditable(entity);

        if (entity.Status == (byte)ScheduledPickupStatus.Promoted)
            throw new InvalidOperationException("Already moved to acquired vehicles.");

        entity.Status = (byte)ScheduledPickupStatus.PickedUp;
        entity.PickedUpAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _audit.WriteForUserAsync(
            driver.Id,
            "PickupMarkedPickedUp",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Chofer marcó recogido: {entity.Vin}",
            $"Driver {driver.DisplayName}",
            ct);
    }

    public async Task MarkNotPickedUpAsync(Guid token, int scheduledId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveDriverByTokenAsync(db, token, ct);
        var entity = await db.ScheduledVehiclePickups
            .FirstOrDefaultAsync(s => s.Id == scheduledId && s.AssignedDriverUserId == driver.Id, ct)
            ?? throw new InvalidOperationException("Pickup not found.");

        if (entity.Status != (byte)ScheduledPickupStatus.PickedUp)
            throw new InvalidOperationException("Only picked-up items can be reverted.");

        EnsureSameDayEditable(entity);

        entity.Status = (byte)ScheduledPickupStatus.Pending;
        entity.PickedUpAtUtc = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await _audit.WriteForUserAsync(
            driver.Id,
            "PickupMarkReverted",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Chofer revirtió recogido: {entity.Vin}",
            $"Driver {driver.DisplayName}",
            ct);
    }

    public async Task<List<DriverReportRow>> GetDriverReportAsync(
        int? driverUserId,
        bool includePending,
        bool includePickedUp,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        if (!includePending && !includePickedUp)
            throw new InvalidOperationException("Elige pendientes, recogidos, o ambos.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var q = db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.AssignedDriver)
            .Include(s => s.VehicleSource)
            .AsQueryable();

        if (driverUserId is > 0)
            q = q.Where(s => s.AssignedDriverUserId == driverUserId);

        if (from is not null)
            q = q.Where(s => s.ScheduledPickupDate >= from);
        if (to is not null)
            q = q.Where(s => s.ScheduledPickupDate <= to);

        var statuses = new List<byte>();
        if (includePending) statuses.Add((byte)ScheduledPickupStatus.Pending);
        if (includePickedUp)
        {
            statuses.Add((byte)ScheduledPickupStatus.PickedUp);
            statuses.Add((byte)ScheduledPickupStatus.Promoted);
        }

        q = q.Where(s => statuses.Contains(s.Status));

        var rows = await q
            .OrderBy(s => s.ScheduledPickupDate)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        rows = rows
            .OrderBy(s => s.AssignedDriver?.DisplayName ?? "\uFFFF")
            .ThenBy(s => s.ScheduledPickupDate)
            .ThenBy(s => s.Id)
            .ToList();

        return rows.Select(s => new DriverReportRow(
            s.Id,
            s.AssignedDriver?.DisplayName ?? "Sin asignar",
            s.Vin,
            s.Year,
            s.Make,
            s.Model,
            s.PurchasePrice,
            s.PickupAddress,
            s.ScheduledPickupDate,
            s.ScheduledPickupWindow,
            (ScheduledPickupStatus)s.Status,
            s.PickedUpAtUtc,
            s.PickedUpAtUtc is null ? null : YardTimeZone.ToEastern(s.PickedUpAtUtc.Value),
            s.PromotedVehicleId,
            s.VehicleSource.Name)).ToList();
    }

    /// <summary>
    /// Promotes picked-up schedules into Vehicles after 12:00 Eastern.
    /// Safe to call repeatedly (idempotent).
    /// </summary>
    public async Task<int> PromoteDuePickupsAsync(CancellationToken ct = default)
    {
        var nowEt = YardTimeZone.NowEastern();
        if (nowEt.TimeOfDay < TimeSpan.FromHours(12))
            return 0;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var due = await db.ScheduledVehiclePickups
            .Include(s => s.AssignedDriver)
            .Include(s => s.Images)
            .Where(s => s.Status == (byte)ScheduledPickupStatus.PickedUp)
            .ToListAsync(ct);

        var count = 0;
        foreach (var s in due)
        {
            if (await db.Vehicles.AnyAsync(v => v.Vin == s.Vin, ct))
            {
                // Already acquired somehow — just mark promoted if we can find it.
                var existing = await db.Vehicles.FirstAsync(v => v.Vin == s.Vin, ct);
                s.Status = (byte)ScheduledPickupStatus.Promoted;
                s.PromotedVehicleId = existing.Id;
                s.PromotedAtUtc = DateTime.UtcNow;
                s.UpdatedAtUtc = DateTime.UtcNow;
                count++;
                continue;
            }

            var acquiredAt = s.PickedUpAtUtc is null
                ? s.ScheduledPickupDate.ToDateTime(TimeOnly.MinValue)
                : DateOnly.FromDateTime(YardTimeZone.ToEastern(s.PickedUpAtUtc.Value))
                    .ToDateTime(TimeOnly.MinValue);

            var vehicle = new Vehicle
            {
                Vin = s.Vin,
                Year = s.Year,
                Make = s.Make,
                Model = s.Model,
                TransmissionType = s.TransmissionType,
                DriveType = s.DriveType,
                Mileage = s.Mileage,
                PurchasePrice = s.PurchasePrice,
                Observations = s.Observations,
                AcquisitionLocation = s.PickupAddress,
                SellerName = s.SellerName,
                SellerPhone = s.SellerPhone,
                SellerEmail = s.SellerEmail,
                PickupDriver = s.AssignedDriver?.DisplayName,
                PaymentMethod = s.PaymentMethod,
                VehicleSourceId = s.VehicleSourceId,
                AcquiredAt = acquiredAt,
                AcquiredByUserId = s.CreatedByUserId,
                PalletId = null,
                ImageRelativePath = s.ImageRelativePath,
                CreatedAtUtc = DateTime.UtcNow
            };

            db.Vehicles.Add(vehicle);
            await db.SaveChangesAsync(ct);

            foreach (var img in s.Images.OrderBy(i => i.SortOrder))
            {
                db.VehicleImages.Add(new VehicleImage
                {
                    VehicleId = vehicle.Id,
                    RelativePath = img.RelativePath,
                    SortOrder = img.SortOrder,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            db.InventoryMovements.Add(new InventoryMovement
            {
                MovementType = (int)MovementType.Acquired,
                VehicleId = vehicle.Id,
                UserId = s.CreatedByUserId,
                Notes = $"Promovido desde agenda de recolección #{s.Id}",
                MovedAtUtc = DateTime.UtcNow
            });

            s.Status = (byte)ScheduledPickupStatus.Promoted;
            s.PromotedVehicleId = vehicle.Id;
            s.PromotedAtUtc = DateTime.UtcNow;
            s.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await _audit.WriteForUserAsync(
                s.CreatedByUserId,
                "PickupPromotedToAcquired",
                "ScheduledVehiclePickup",
                s.Id,
                $"Agenda promovida a adquiridos: {s.Vin}",
                $"VehicleId={vehicle.Id} (12:00 ET)",
                ct);

            count++;
        }

        return count;
    }

    public static bool CanDriverEditToday(ScheduledVehiclePickup entity)
    {
        if (entity.Status == (byte)ScheduledPickupStatus.Promoted)
            return false;

        if (entity.Status == (byte)ScheduledPickupStatus.Pending)
            return true;

        if (entity.PickedUpAtUtc is null)
            return true;

        var pickedEt = YardTimeZone.ToEastern(entity.PickedUpAtUtc.Value);
        return DateOnly.FromDateTime(pickedEt) == YardTimeZone.TodayEastern();
    }

    public static IReadOnlyList<string> GetImagePaths(ScheduledVehiclePickup s)
    {
        if (s.Images is { Count: > 0 })
            return s.Images.OrderBy(i => i.SortOrder).Select(i => i.RelativePath).ToList();
        if (!string.IsNullOrWhiteSpace(s.ImageRelativePath))
            return [s.ImageRelativePath];
        return [];
    }

    private static void EnsureSameDayEditable(ScheduledVehiclePickup entity)
    {
        if (!CanDriverEditToday(entity))
            throw new InvalidOperationException(
                "This pickup can no longer be changed after the collection day has ended.");
    }

    private static async Task<User> ResolveDriverByTokenAsync(
        YardInventoryDbContext db,
        Guid token,
        CancellationToken ct)
    {
        var driver = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.DriverAccessToken == token && u.IsActive, ct)
            ?? throw new UnauthorizedAccessException("Invalid driver link.");

        if (!driver.Roles.Any(r => r.Code == (int)AppRole.Driver))
            throw new UnauthorizedAccessException("Invalid driver link.");

        return driver;
    }

    private void EnsureCanManageSchedule()
    {
        if (!_currentUser.CanManagePickupSchedule)
            throw new UnauthorizedAccessException("No tiene permiso para la agenda de recolección.");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record DriverPortalSnapshot(User Driver, List<ScheduledVehiclePickup> ActivePickups);

public sealed record DriverReportRow(
    int Id,
    string DriverName,
    string Vin,
    int? Year,
    string? Make,
    string? Model,
    decimal? PurchasePrice,
    string? PickupAddress,
    DateOnly ScheduledPickupDate,
    string? ScheduledPickupWindow,
    ScheduledPickupStatus Status,
    DateTime? PickedUpAtUtc,
    DateTime? PickedUpAtEastern,
    int? PromotedVehicleId,
    string SourceName);
