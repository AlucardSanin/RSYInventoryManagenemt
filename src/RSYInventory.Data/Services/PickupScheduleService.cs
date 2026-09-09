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
    private readonly VehiclePriceHistoryService _priceHistory;

    public PickupScheduleService(
        IDbContextFactory<YardInventoryDbContext> dbFactory,
        ICurrentUserService currentUser,
        AuditService audit,
        VehiclePriceHistoryService priceHistory)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
        _audit = audit;
        _priceHistory = priceHistory;
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

    /// <summary>
    /// Users for "Creado por": active users who can schedule pickups or register acquisitions
    /// (same privilege as <see cref="ICurrentUserService.CanManagePickupSchedule"/> /
    /// <see cref="ICurrentUserService.CanAcquireVehicles"/>).
    /// </summary>
    public async Task<List<User>> GetScheduleCreatorUsersAsync(CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // CanAcquireVehicles / CanManagePickupSchedule:
        // VehicleAcquirer | InventoryEditor | ZoneManager | SystemAdmin
        var scheduleRoleCodes = new[]
        {
            (int)AppRole.VehicleAcquirer,
            (int)AppRole.InventoryEditor,
            (int)AppRole.ZoneManager,
            (int)AppRole.SystemAdmin
        };

        return await db.Users
            .AsNoTracking()
            .Where(u => u.IsActive && u.Roles.Any(r => scheduleRoleCodes.Contains(r.Code)))
            .OrderBy(u => u.DisplayName)
            .ToListAsync(ct);
    }

    public async Task<List<ScheduledVehiclePickup>> GetActiveScheduleAsync(CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Recogido rows stay visible only until midnight Eastern (NC); then they leave the agenda list.
        var today = YardTimeZone.TodayEastern();
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            today.ToDateTime(TimeOnly.MinValue),
            YardTimeZone.EasternInfo);
        var nextDayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            today.AddDays(1).ToDateTime(TimeOnly.MinValue),
            YardTimeZone.EasternInfo);

        return await db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.AssignedDriver)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Images)
            .Where(s =>
                s.Status == (byte)ScheduledPickupStatus.Pending
                || ((s.Status == (byte)ScheduledPickupStatus.PickedUp
                     || s.Status == (byte)ScheduledPickupStatus.Promoted)
                    && (
                        (s.PickedUpAtUtc != null
                         && s.PickedUpAtUtc >= dayStartUtc
                         && s.PickedUpAtUtc < nextDayStartUtc)
                        || (s.PickedUpAtUtc == null
                            && s.PromotedAtUtc != null
                            && s.PromotedAtUtc >= dayStartUtc
                            && s.PromotedAtUtc < nextDayStartUtc)
                        || (s.PickedUpAtUtc == null
                            && s.PromotedAtUtc == null
                            && s.ScheduledPickupDate == today)
                    )))
            .OrderBy(s => s.Status == (byte)ScheduledPickupStatus.Pending ? 0 : 1)
            .ThenBy(s => s.ScheduledPickupDate)
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
        var previousPrice = entity.PurchasePrice;
        var nextPrice = VehiclePriceHistoryService.Normalize(purchasePrice);

        entity.Vin = vin;
        entity.Year = year;
        entity.Make = NullIfWhiteSpace(make);
        entity.Model = NullIfWhiteSpace(model);
        entity.TransmissionType = transmissionType is null ? null : (int)transmissionType;
        entity.DriveType = driveType is null ? null : (int)driveType;
        entity.Mileage = mileage;
        entity.PurchasePrice = nextPrice;
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

        _priceHistory.AddScheduleChange(db, entity.Id, previousPrice, nextPrice);
        await db.SaveChangesAsync(ct);
        await _priceHistory.WriteAuditForScheduleAsync(entity.Id, previousPrice, nextPrice, ct);

        await _audit.WriteAsync(
            "PickupScheduleUpdated",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Agenda editada: {entity.Vin}",
            previousVin == entity.Vin
                ? $"Fecha {entity.ScheduledPickupDate:yyyy-MM-dd}"
                  + (string.IsNullOrWhiteSpace(entity.ScheduledPickupWindow) ? "" : $" · {entity.ScheduledPickupWindow}")
                  + (VehiclePriceHistoryService.HasChanged(previousPrice, nextPrice)
                      ? $" · precio {previousPrice?.ToString("0.00") ?? "—"} → {nextPrice?.ToString("0.00") ?? "—"}"
                      : "")
                : $"VIN {previousVin} → {entity.Vin}; fecha {entity.ScheduledPickupDate:yyyy-MM-dd}",
            ct);
    }

    public Task<IReadOnlyList<VehiclePriceHistory>> GetPriceHistoryAsync(
        int scheduledPickupId,
        CancellationToken ct = default)
        => _priceHistory.GetForScheduleAsync(scheduledPickupId, ct);

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

        // Drivers only see today's agenda (NC local) — pending and already picked (for revert).
        var today = YardTimeZone.TodayEastern();
        var active = await db.ScheduledVehiclePickups
            .AsNoTracking()
            .Include(s => s.VehicleSource)
            .Include(s => s.Images)
            .Where(s => s.AssignedDriverUserId == driver.Id
                        && s.ScheduledPickupDate == today)
            .OrderBy(s => s.Status == (byte)ScheduledPickupStatus.Pending ? 0 : 1)
            .ThenBy(s => s.ScheduledPickupWindow)
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

        // Prefer actual pickup day (ET); fall back to scheduled date.
        if (from is not null || to is not null)
        {
            var fromDate = from ?? DateOnly.MinValue;
            var toDate = to ?? DateOnly.MaxValue;
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), YardTimeZone.EasternInfo);
            var toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(
                toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                YardTimeZone.EasternInfo);

            q = q.Where(s =>
                (s.PickedUpAtUtc != null
                 && s.PickedUpAtUtc >= fromUtc
                 && s.PickedUpAtUtc < toUtcExclusive)
                || (s.PickedUpAtUtc == null
                    && s.PromotedAtUtc != null
                    && s.PromotedAtUtc >= fromUtc
                    && s.PromotedAtUtc < toUtcExclusive)
                || (s.PickedUpAtUtc == null
                    && s.PromotedAtUtc == null
                    && s.ScheduledPickupDate >= fromDate
                    && s.ScheduledPickupDate <= toDate));
        }

        var scheduled = await q
            .OrderByDescending(s => s.PickedUpAtUtc ?? s.PromotedAtUtc)
            .ThenByDescending(s => s.Id)
            .ToListAsync(ct);

        var promotedIds = scheduled
            .Where(s => s.PromotedVehicleId is not null)
            .Select(s => s.PromotedVehicleId!.Value)
            .ToHashSet();

        // Also include acquired vehicles linked to this driver (manual acquire / legacy text).
        var vehicleQuery = db.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleSource)
            .Include(v => v.Images)
            .Where(v => v.DeletedAtUtc == null
                        && v.PickupDriverUserId == driver.Id
                        && !promotedIds.Contains(v.Id));

        if (from is not null)
        {
            var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
            vehicleQuery = vehicleQuery.Where(v => v.AcquiredAt >= fromDt);
        }

        if (to is not null)
        {
            var toExclusive = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            vehicleQuery = vehicleQuery.Where(v => v.AcquiredAt < toExclusive);
        }

        var vehicles = await vehicleQuery
            .OrderByDescending(v => v.AcquiredAt)
            .ThenByDescending(v => v.Id)
            .ToListAsync(ct);

        var fromVehicles = vehicles.Select(ToHistoryPickup).ToList();

        return scheduled
            .Concat(fromVehicles)
            .OrderByDescending(s => s.PickedUpAtUtc ?? s.PromotedAtUtc ?? s.ScheduledPickupDate.ToDateTime(TimeOnly.MinValue))
            .ThenByDescending(s => Math.Abs(s.Id))
            .ToList();
    }

    /// <summary>
    /// Builds a read-only schedule-shaped row for driver history from an acquired vehicle.
    /// Negative Id marks it as not editable via schedule actions.
    /// </summary>
    private static ScheduledVehiclePickup ToHistoryPickup(Vehicle v)
    {
        var acquiredDate = DateOnly.FromDateTime(v.AcquiredAt);
        var pickedUtc = TimeZoneInfo.ConvertTimeToUtc(
            acquiredDate.ToDateTime(new TimeOnly(12, 0)),
            YardTimeZone.EasternInfo);

        return new ScheduledVehiclePickup
        {
            Id = -v.Id,
            Vin = v.Vin,
            Year = v.Year,
            Make = v.Make,
            Model = v.Model,
            TransmissionType = v.TransmissionType,
            DriveType = v.DriveType,
            Mileage = v.Mileage,
            PurchasePrice = v.PurchasePrice,
            Observations = v.Observations,
            PickupAddress = v.AcquisitionLocation,
            SellerName = v.SellerName,
            SellerPhone = v.SellerPhone,
            SellerEmail = v.SellerEmail,
            PaymentMethod = v.PaymentMethod,
            VehicleSourceId = v.VehicleSourceId,
            VehicleSource = v.VehicleSource,
            ScheduledPickupDate = acquiredDate,
            AssignedDriverUserId = v.PickupDriverUserId,
            Status = (byte)ScheduledPickupStatus.Promoted,
            PickedUpAtUtc = pickedUtc,
            PromotedAtUtc = pickedUtc,
            PromotedVehicleId = v.Id,
            ImageRelativePath = v.ImageRelativePath,
            Images = (v.Images ?? [])
                .OrderBy(i => i.SortOrder)
                .Select(i => new ScheduledVehiclePickupImage
                {
                    RelativePath = i.RelativePath,
                    SortOrder = i.SortOrder,
                    CreatedAtUtc = i.CreatedAtUtc
                })
                .ToList(),
            CreatedAtUtc = v.CreatedAtUtc,
            CreatedByUserId = v.AcquiredByUserId
        };
    }

    public async Task MarkPickedUpAsync(Guid token, int scheduledId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveDriverByTokenAsync(db, token, ct);
        var entity = await db.ScheduledVehiclePickups
            .Include(s => s.AssignedDriver)
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == scheduledId && s.AssignedDriverUserId == driver.Id, ct)
            ?? throw new InvalidOperationException("Pickup not found.");

        EnsureSameDayEditable(entity);

        if (entity.Status != (byte)ScheduledPickupStatus.Pending)
            throw new InvalidOperationException("Already marked as picked up.");

        entity.PickedUpAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await PromoteToAcquiredInternalAsync(db, entity, actingUserId: driver.Id, ct);

        await _audit.WriteForUserAsync(
            driver.Id,
            "PickupMarkedPickedUp",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Chofer marcó recogido: {entity.Vin}",
            $"Driver {driver.DisplayName}; VehicleId={entity.PromotedVehicleId}",
            ct);
    }

    public async Task MarkNotPickedUpAsync(Guid token, int scheduledId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var driver = await ResolveDriverByTokenAsync(db, token, ct);
        var entity = await db.ScheduledVehiclePickups
            .FirstOrDefaultAsync(s => s.Id == scheduledId && s.AssignedDriverUserId == driver.Id, ct)
            ?? throw new InvalidOperationException("Pickup not found.");

        EnsureSameDayEditable(entity);
        await RevertToPendingInternalAsync(db, entity, ct);

        await _audit.WriteForUserAsync(
            driver.Id,
            "PickupMarkReverted",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Chofer revirtió recogido: {entity.Vin}",
            $"Driver {driver.DisplayName}",
            ct);
    }

    /// <summary>Staff: mark a pending schedule as picked up and add it to acquired vehicles now.</summary>
    public async Task StaffMarkPickedUpAsync(int scheduledId, CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledVehiclePickups
            .Include(s => s.AssignedDriver)
            .Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.Id == scheduledId, ct)
            ?? throw new InvalidOperationException("Agenda no encontrada.");

        if (entity.Status != (byte)ScheduledPickupStatus.Pending)
            throw new InvalidOperationException("Solo se pueden marcar como recogidos los pendientes.");

        entity.PickedUpAtUtc = DateTime.UtcNow;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await PromoteToAcquiredInternalAsync(db, entity, actingUserId: _currentUser.UserId, ct);

        await _audit.WriteAsync(
            "PickupMarkedPickedUp",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Marcado recogido (staff): {entity.Vin}",
            $"VehicleId={entity.PromotedVehicleId}",
            ct);
    }

    /// <summary>Staff: revert a picked-up schedule back to pending and remove it from acquired vehicles.</summary>
    public async Task StaffRevertToPendingAsync(int scheduledId, CancellationToken ct = default)
    {
        EnsureCanManageSchedule();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.ScheduledVehiclePickups
            .FirstOrDefaultAsync(s => s.Id == scheduledId, ct)
            ?? throw new InvalidOperationException("Agenda no encontrada.");

        await RevertToPendingInternalAsync(db, entity, ct);

        await _audit.WriteAsync(
            "PickupMarkReverted",
            "ScheduledVehiclePickup",
            entity.Id,
            $"Revertido a pendiente (staff): {entity.Vin}",
            null,
            ct);
    }

    public async Task<List<DriverReportRow>> GetDriverReportAsync(
        int? driverUserId,
        int? createdByUserId,
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
            .Include(s => s.CreatedByUser)
            .Include(s => s.VehicleSource)
            .AsQueryable();

        if (driverUserId is > 0)
            q = q.Where(s => s.AssignedDriverUserId == driverUserId);

        if (createdByUserId is > 0)
            q = q.Where(s => s.CreatedByUserId == createdByUserId);

        var statuses = new List<byte>();
        if (includePending) statuses.Add((byte)ScheduledPickupStatus.Pending);
        if (includePickedUp)
        {
            statuses.Add((byte)ScheduledPickupStatus.PickedUp);
            statuses.Add((byte)ScheduledPickupStatus.Promoted);
        }

        q = q.Where(s => statuses.Contains(s.Status));

        // Date: scheduled day in range, or actual pickup day (ET) in range.
        if (from is not null || to is not null)
        {
            var fromDate = from ?? DateOnly.MinValue;
            var toDate = to ?? DateOnly.MaxValue;
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(fromDate.ToDateTime(TimeOnly.MinValue), YardTimeZone.EasternInfo);
            var toUtcExclusive = TimeZoneInfo.ConvertTimeToUtc(
                toDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                YardTimeZone.EasternInfo);

            q = q.Where(s =>
                (s.ScheduledPickupDate >= fromDate && s.ScheduledPickupDate <= toDate)
                || (s.PickedUpAtUtc != null
                    && s.PickedUpAtUtc >= fromUtc
                    && s.PickedUpAtUtc < toUtcExclusive)
                || (s.PickedUpAtUtc == null
                    && s.PromotedAtUtc != null
                    && s.PromotedAtUtc >= fromUtc
                    && s.PromotedAtUtc < toUtcExclusive));
        }

        var scheduled = await q.ToListAsync(ct);

        var rows = scheduled.Select(s => new DriverReportRow(
            s.Id,
            s.AssignedDriver?.DisplayName ?? "Sin asignar",
            s.CreatedByUser.DisplayName,
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

        // Acquired vehicles linked to a driver (manual entry / backfill) that are not already in schedule rows.
        // Kept when including recogidos so Vehicles-section cars still appear in-range.
        if (includePickedUp)
        {
            var promotedIds = rows
                .Where(r => r.PromotedVehicleId is not null)
                .Select(r => r.PromotedVehicleId!.Value)
                .ToHashSet();

            var vehicleQuery = db.Vehicles
                .AsNoTracking()
                .Include(v => v.PickupDriverUser)
                .Include(v => v.AcquiredByUser)
                .Include(v => v.VehicleSource)
                .Where(v => v.DeletedAtUtc == null
                            && v.PickupDriverUserId != null
                            && !promotedIds.Contains(v.Id));

            if (driverUserId is > 0)
                vehicleQuery = vehicleQuery.Where(v => v.PickupDriverUserId == driverUserId);

            // Agenda-creator filter: only vehicles promoted from that user's schedules
            // (or acquired by them when there is no remaining schedule row).
            if (createdByUserId is > 0)
            {
                var creatorId = createdByUserId.Value;
                var vehicleIdsFromCreator = await db.ScheduledVehiclePickups
                    .AsNoTracking()
                    .Where(s => s.CreatedByUserId == creatorId && s.PromotedVehicleId != null)
                    .Select(s => s.PromotedVehicleId!.Value)
                    .ToListAsync(ct);
                vehicleQuery = vehicleQuery.Where(v =>
                    vehicleIdsFromCreator.Contains(v.Id) || v.AcquiredByUserId == creatorId);
            }

            if (from is not null)
            {
                var fromDt = from.Value.ToDateTime(TimeOnly.MinValue);
                vehicleQuery = vehicleQuery.Where(v => v.AcquiredAt >= fromDt);
            }

            if (to is not null)
            {
                var toExclusive = to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
                vehicleQuery = vehicleQuery.Where(v => v.AcquiredAt < toExclusive);
            }

            var vehicles = await vehicleQuery.ToListAsync(ct);
            foreach (var v in vehicles)
            {
                var acquiredDate = DateOnly.FromDateTime(v.AcquiredAt);
                var pickedUtc = TimeZoneInfo.ConvertTimeToUtc(
                    acquiredDate.ToDateTime(new TimeOnly(12, 0)),
                    YardTimeZone.EasternInfo);

                rows.Add(new DriverReportRow(
                    -v.Id,
                    v.PickupDriverUser?.DisplayName ?? v.PickupDriver ?? "Sin asignar",
                    v.AcquiredByUser?.DisplayName ?? "—",
                    v.Vin,
                    v.Year,
                    v.Make,
                    v.Model,
                    v.PurchasePrice,
                    v.AcquisitionLocation,
                    acquiredDate,
                    null,
                    ScheduledPickupStatus.Promoted,
                    pickedUtc,
                    YardTimeZone.ToEastern(pickedUtc),
                    v.Id,
                    v.VehicleSource.Name));
            }
        }

        return rows
            .OrderBy(r => r.DriverName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ScheduledPickupDate)
            .ThenBy(r => r.Vin, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Legacy no-op: promotion now happens immediately when marking picked up.
    /// Kept so old hosts that still register the background service do not break.
    /// </summary>
    public Task<int> PromoteDuePickupsAsync(CancellationToken ct = default) =>
        Task.FromResult(0);

    private async Task PromoteToAcquiredInternalAsync(
        YardInventoryDbContext db,
        ScheduledVehiclePickup s,
        int actingUserId,
        CancellationToken ct)
    {
        if (s.Status == (byte)ScheduledPickupStatus.Promoted && s.PromotedVehicleId is not null)
            return;

        if (await db.Vehicles.AnyAsync(v => v.Vin == s.Vin && v.DeletedAtUtc == null, ct))
        {
            var existing = await db.Vehicles.FirstAsync(v => v.Vin == s.Vin && v.DeletedAtUtc == null, ct);
            s.Status = (byte)ScheduledPickupStatus.Promoted;
            s.PromotedVehicleId = existing.Id;
            s.PromotedAtUtc = DateTime.UtcNow;
            s.PickedUpAtUtc ??= DateTime.UtcNow;
            s.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        // Ensure navigation for driver name if not loaded.
        if (s.AssignedDriverUserId is not null && s.AssignedDriver is null)
        {
            await db.Entry(s).Reference(x => x.AssignedDriver).LoadAsync(ct);
        }

        if (s.Images.Count == 0)
            await db.Entry(s).Collection(x => x.Images).LoadAsync(ct);

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
            PickupDriverUserId = s.AssignedDriverUserId,
            PickupDriver = s.AssignedDriver?.DisplayName,
            PaymentMethod = s.PaymentMethod,
            VehicleSourceId = s.VehicleSourceId,
            AcquiredAt = acquiredAt,
            AcquiredByUserId = actingUserId > 0 ? actingUserId : s.CreatedByUserId,
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
            UserId = actingUserId > 0 ? actingUserId : s.CreatedByUserId,
            Notes = $"Recogido desde agenda de recolección #{s.Id}",
            MovedAtUtc = DateTime.UtcNow
        });

        await _priceHistory.CopyScheduleHistoryToVehicleAsync(db, s.Id, vehicle.Id, ct);

        s.Status = (byte)ScheduledPickupStatus.Promoted;
        s.PromotedVehicleId = vehicle.Id;
        s.PromotedAtUtc = DateTime.UtcNow;
        s.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task RevertToPendingInternalAsync(
        YardInventoryDbContext db,
        ScheduledVehiclePickup entity,
        CancellationToken ct)
    {
        var isPicked = entity.Status is (byte)ScheduledPickupStatus.PickedUp
            or (byte)ScheduledPickupStatus.Promoted;
        if (!isPicked)
            throw new InvalidOperationException("Solo se puede revertir un vehículo ya marcado como recogido.");

        if (entity.PromotedVehicleId is int vehicleId)
        {
            var vehicle = await db.Vehicles
                .Include(v => v.Images)
                .FirstOrDefaultAsync(v => v.Id == vehicleId, ct);

            if (vehicle is not null)
            {
                if (vehicle.PalletId is not null)
                    throw new InvalidOperationException(
                        "No se puede revertir: el vehículo ya tiene ubicación en la yarda.");
                if (vehicle.InvoiceNumber is not null || vehicle.SignedAtUtc is not null)
                    throw new InvalidOperationException(
                        "No se puede revertir: el vehículo ya tiene recibo o firma.");

                var movements = await db.InventoryMovements
                    .Where(m => m.VehicleId == vehicle.Id)
                    .ToListAsync(ct);
                db.InventoryMovements.RemoveRange(movements);
                db.VehicleImages.RemoveRange(vehicle.Images);
                db.Vehicles.Remove(vehicle);
            }
        }

        entity.Status = (byte)ScheduledPickupStatus.Pending;
        entity.PickedUpAtUtc = null;
        entity.PromotedVehicleId = null;
        entity.PromotedAtUtc = null;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public static bool CanDriverEditToday(ScheduledVehiclePickup entity)
    {
        if (entity.Status == (byte)ScheduledPickupStatus.Pending)
            return entity.ScheduledPickupDate == YardTimeZone.TodayEastern();

        // Picked up / promoted: allow same-calendar-day revert (NC).
        if (entity.PickedUpAtUtc is null)
            return entity.ScheduledPickupDate == YardTimeZone.TodayEastern();

        var pickedEt = YardTimeZone.ToEastern(entity.PickedUpAtUtc.Value);
        return DateOnly.FromDateTime(pickedEt) == YardTimeZone.TodayEastern();
    }

    public static bool IsPickedUpStatus(byte status) =>
        status is (byte)ScheduledPickupStatus.PickedUp or (byte)ScheduledPickupStatus.Promoted;

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
    string CreatedByName,
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
