using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public class VehicleService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;

    public VehicleService(IDbContextFactory<YardInventoryDbContext> dbFactory, ICurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<List<VehicleSource>> GetSourcesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.VehicleSources
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<List<string>> GetDistinctMakesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var stored = await db.Vehicles
            .AsNoTracking()
            .Where(v => v.Make != null && v.Make != "")
            .Select(v => v.Make!)
            .Distinct()
            .ToListAsync(ct);
        return VehicleMakeCatalog.Merge(stored);
    }

    public async Task<List<string>> GetDistinctModelsAsync(string? make = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var query = db.Vehicles.AsNoTracking().Where(v => v.Model != null && v.Model != "");
        if (!string.IsNullOrWhiteSpace(make))
        {
            var m = make.Trim();
            query = query.Where(v => v.Make != null && v.Make.ToLower() == m.ToLower());
        }

        return await query
            .Select(v => v.Model!)
            .Distinct()
            .OrderBy(x => x)
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<List<Vehicle>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleSource)
            .Include(v => v.AcquiredByUser)
            .Include(v => v.Images)
            .Include(v => v.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .OrderByDescending(v => v.AcquiredAt)
            .ToListAsync(ct);
    }

    public async Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Vehicles
            .Include(v => v.VehicleSource)
            .Include(v => v.Images)
            .Include(v => v.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    public async Task<Vehicle> AcquireAsync(
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
        DateTime acquiredAt,
        CancellationToken ct = default)
    {
        if (!_currentUser.CanAcquireVehicles && !_currentUser.CanEditInventory)
            throw new UnauthorizedAccessException("No tiene permiso para registrar vehículos.");

        vin = vin.Trim().ToUpperInvariant();
        if (vin.Length is < 11 or > 17)
            throw new InvalidOperationException("El VIN debe tener entre 11 y 17 caracteres.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (await db.Vehicles.AnyAsync(v => v.Vin == vin, ct))
            throw new InvalidOperationException("Ya existe un vehículo con ese VIN.");

        if (!await db.VehicleSources.AnyAsync(s => s.Id == vehicleSourceId && s.IsActive, ct))
            throw new InvalidOperationException("Fuente de adquisición inválida.");

        var vehicle = new Vehicle
        {
            Vin = vin,
            Year = year,
            Make = make,
            Model = model,
            TransmissionType = transmissionType is null ? null : (int)transmissionType,
            DriveType = driveType is null ? null : (int)driveType,
            Mileage = mileage,
            PurchasePrice = purchasePrice is null or <= 0 ? null : Math.Round(purchasePrice.Value, 2, MidpointRounding.AwayFromZero),
            Observations = observations,
            VehicleSourceId = vehicleSourceId,
            AcquiredAt = acquiredAt,
            AcquiredByUserId = _currentUser.UserId,
            PalletId = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync(ct);

        db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = (int)MovementType.Acquired,
            VehicleId = vehicle.Id,
            UserId = _currentUser.UserId,
            Notes = $"Adquirido desde fuente #{vehicleSourceId}",
            MovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return vehicle;
    }

    public async Task SetImagePathAsync(int vehicleId, string? relativePath, CancellationToken ct = default)
    {
        if (!_currentUser.CanAcquireVehicles && !_currentUser.CanEditInventory)
            throw new UnauthorizedAccessException("No tiene permiso para actualizar vehículos.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var vehicle = await db.Vehicles
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        vehicle.ImageRelativePath = relativePath;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(relativePath)
            && !vehicle.Images.Any(i => string.Equals(i.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)))
        {
            var nextOrder = vehicle.Images.Count == 0 ? 0 : vehicle.Images.Max(i => i.SortOrder) + 1;
            db.VehicleImages.Add(new VehicleImage
            {
                VehicleId = vehicleId,
                RelativePath = relativePath,
                SortOrder = nextOrder,
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Appends a photo to the vehicle gallery and keeps ImageRelativePath as the cover (first) image.</summary>
    public async Task AddImageAsync(int vehicleId, string relativePath, CancellationToken ct = default)
    {
        if (!_currentUser.CanAcquireVehicles && !_currentUser.CanEditInventory)
            throw new UnauthorizedAccessException("No tiene permiso para actualizar vehículos.");

        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Ruta de imagen inválida.");

        relativePath = relativePath.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var vehicle = await db.Vehicles
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        if (vehicle.Images.Any(i => string.Equals(i.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var nextOrder = vehicle.Images.Count == 0 ? 0 : vehicle.Images.Max(i => i.SortOrder) + 1;
        db.VehicleImages.Add(new VehicleImage
        {
            VehicleId = vehicleId,
            RelativePath = relativePath,
            SortOrder = nextOrder,
            CreatedAtUtc = DateTime.UtcNow
        });

        if (string.IsNullOrWhiteSpace(vehicle.ImageRelativePath))
            vehicle.ImageRelativePath = relativePath;

        vehicle.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Ordered gallery paths (falls back to legacy ImageRelativePath when gallery is empty).</summary>
    public static IReadOnlyList<string> GetImagePaths(Vehicle vehicle)
    {
        var fromGallery = (vehicle.Images ?? [])
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .Select(i => i.RelativePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fromGallery.Count > 0)
            return fromGallery;

        return string.IsNullOrWhiteSpace(vehicle.ImageRelativePath)
            ? []
            : [vehicle.ImageRelativePath];
    }

    public async Task AssignLocationAsync(int vehicleId, int palletId, string? notes, CancellationToken ct = default)
    {
        if (!_currentUser.CanEditInventory && !_currentUser.CanManageZones)
            throw new UnauthorizedAccessException("No tiene permiso para ubicar vehículos.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var pallet = await db.Pallets
            .Include(p => p.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(p => p.Id == palletId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Paleta no encontrada o inactiva.");

        if (pallet.Row.Zone.Purpose != (int)ZonePurpose.Vehicles)
            throw new InvalidOperationException("Solo se pueden ubicar vehículos en zonas de vehículos.");

        var fromPalletId = vehicle.PalletId;
        vehicle.PalletId = palletId;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;

        db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = (int)(fromPalletId is null ? MovementType.Assigned : MovementType.Relocated),
            VehicleId = vehicle.Id,
            FromPalletId = fromPalletId,
            ToPalletId = palletId,
            UserId = _currentUser.UserId,
            Notes = notes ?? (fromPalletId is null ? "Ubicación asignada" : "Vehículo reubicado"),
            MovedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
    }

    public static (int? Year, string? Make) TryDecodeVin(string? vin) => VinDecoder.TryDecode(vin);
}
