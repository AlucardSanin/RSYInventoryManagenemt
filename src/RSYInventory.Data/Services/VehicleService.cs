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
            .AsNoTracking()
            .Include(v => v.VehicleSource)
            .Include(v => v.AcquiredByUser)
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
        string? acquisitionLocation = null,
        string? sellerName = null,
        string? sellerPhone = null,
        string? sellerEmail = null,
        string? pickupDriver = null,
        string? paymentMethod = null,
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
            AcquisitionLocation = NullIfWhiteSpace(acquisitionLocation),
            SellerName = NullIfWhiteSpace(sellerName),
            SellerPhone = NullIfWhiteSpace(sellerPhone),
            SellerEmail = NullIfWhiteSpace(sellerEmail),
            PickupDriver = NullIfWhiteSpace(pickupDriver),
            PaymentMethod = NullIfWhiteSpace(paymentMethod),
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
        if (!_currentUser.CanEditVehicles)
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
        if (!_currentUser.CanEditVehicles)
            throw new UnauthorizedAccessException("No tiene permiso para agregar fotos al vehículo.");

        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Ruta de imagen inválida.");

        relativePath = relativePath.Trim();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var vehicle = await db.Vehicles
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        EnsureLegacyCoverInGallery(vehicle, db);

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

    /// <summary>Removes one gallery photo. Cover falls back to the next remaining image.</summary>
    public async Task RemoveImageAsync(int vehicleId, int imageId, CancellationToken ct = default)
    {
        if (!_currentUser.CanEditVehicles)
            throw new UnauthorizedAccessException("No tiene permiso para eliminar fotos del vehículo.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var vehicle = await db.Vehicles
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        EnsureLegacyCoverInGallery(vehicle, db);
        await db.SaveChangesAsync(ct);

        var image = vehicle.Images.FirstOrDefault(i => i.Id == imageId)
            ?? throw new InvalidOperationException("Imagen no encontrada.");

        var removedPath = image.RelativePath;
        db.VehicleImages.Remove(image);
        await db.SaveChangesAsync(ct);

        // Reload remaining after delete.
        var remaining = await db.VehicleImages
            .Where(i => i.VehicleId == vehicleId)
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

        vehicle = await db.Vehicles.FirstAsync(v => v.Id == vehicleId, ct);
        if (string.Equals(vehicle.ImageRelativePath, removedPath, StringComparison.OrdinalIgnoreCase)
            || remaining.Count == 0
            || !remaining.Any(i => string.Equals(i.RelativePath, vehicle.ImageRelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            vehicle.ImageRelativePath = remaining.FirstOrDefault()?.RelativePath;
        }

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

        if (!string.IsNullOrWhiteSpace(vehicle.ImageRelativePath)
            && !fromGallery.Any(p => string.Equals(p, vehicle.ImageRelativePath, StringComparison.OrdinalIgnoreCase)))
        {
            fromGallery.Insert(0, vehicle.ImageRelativePath!);
        }

        return fromGallery;
    }

    /// <summary>Gallery rows for edit UI (ensures legacy cover appears as a removable item).</summary>
    public async Task<IReadOnlyList<VehicleImage>> GetImagesForEditAsync(int vehicleId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var vehicle = await db.Vehicles
            .Include(v => v.Images)
            .FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        EnsureLegacyCoverInGallery(vehicle, db);
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);

        return vehicle.Images
            .OrderBy(i => i.SortOrder)
            .ThenBy(i => i.Id)
            .ToList();
    }

    private static void EnsureLegacyCoverInGallery(Vehicle vehicle, YardInventoryDbContext db)
    {
        if (string.IsNullOrWhiteSpace(vehicle.ImageRelativePath))
            return;

        if (vehicle.Images.Any(i =>
                string.Equals(i.RelativePath, vehicle.ImageRelativePath, StringComparison.OrdinalIgnoreCase)))
            return;

        var minOrder = vehicle.Images.Count == 0 ? 0 : vehicle.Images.Min(i => i.SortOrder) - 1;
        var row = new VehicleImage
        {
            VehicleId = vehicle.Id,
            RelativePath = vehicle.ImageRelativePath!,
            SortOrder = minOrder,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.VehicleImages.Add(row);
        vehicle.Images.Add(row);
    }

    public async Task UpdateAsync(
        int vehicleId,
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
        string? acquisitionLocation = null,
        string? sellerName = null,
        string? sellerPhone = null,
        string? sellerEmail = null,
        string? pickupDriver = null,
        string? paymentMethod = null,
        CancellationToken ct = default)
    {
        if (!_currentUser.CanEditVehicles)
            throw new UnauthorizedAccessException("No tiene permiso para editar vehículos.");

        vin = vin.Trim().ToUpperInvariant();
        if (vin.Length is < 11 or > 17)
            throw new InvalidOperationException("El VIN debe tener entre 11 y 17 caracteres.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        if (await db.Vehicles.AnyAsync(v => v.Vin == vin && v.Id != vehicleId, ct))
            throw new InvalidOperationException("Ya existe otro vehículo con ese VIN.");

        var source = await db.VehicleSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == vehicleSourceId, ct)
            ?? throw new InvalidOperationException("Fuente de adquisición inválida.");

        if (!source.IsActive && vehicle.VehicleSourceId != vehicleSourceId)
            throw new InvalidOperationException("Fuente de adquisición inválida.");

        vehicle.Vin = vin;
        vehicle.Year = year;
        vehicle.Make = string.IsNullOrWhiteSpace(make) ? null : make.Trim();
        vehicle.Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim();
        vehicle.TransmissionType = transmissionType is null ? null : (int)transmissionType;
        vehicle.DriveType = driveType is null ? null : (int)driveType;
        vehicle.Mileage = mileage;
        vehicle.PurchasePrice = purchasePrice is null or <= 0
            ? null
            : Math.Round(purchasePrice.Value, 2, MidpointRounding.AwayFromZero);
        vehicle.Observations = string.IsNullOrWhiteSpace(observations) ? null : observations.Trim();
        vehicle.AcquisitionLocation = NullIfWhiteSpace(acquisitionLocation);
        vehicle.SellerName = NullIfWhiteSpace(sellerName);
        vehicle.SellerPhone = NullIfWhiteSpace(sellerPhone);
        vehicle.SellerEmail = NullIfWhiteSpace(sellerEmail);
        vehicle.PickupDriver = NullIfWhiteSpace(pickupDriver);
        vehicle.PaymentMethod = NullIfWhiteSpace(paymentMethod);
        vehicle.VehicleSourceId = vehicleSourceId;
        vehicle.AcquiredAt = acquiredAt;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Assigns the next invoice number (from 1000) if the vehicle has none yet.</summary>
    public async Task<int> EnsureInvoiceNumberAsync(int vehicleId, CancellationToken ct = default)
    {
        if (!_currentUser.CanEditVehicles)
            throw new UnauthorizedAccessException("No tiene permiso para generar recibos de compra.");

        await using var strategyDb = await _dbFactory.CreateDbContextAsync(ct);
        var strategy = strategyDb.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
                ?? throw new InvalidOperationException("Vehículo no encontrado.");

            if (vehicle.InvoiceNumber is > 0)
            {
                await tx.CommitAsync(ct);
                return vehicle.InvoiceNumber.Value;
            }

            // Atomically take the next number from the sequence table.
            // EF Core maps scalar SqlQuery results to a column named Value.
            var assigned = await db.Database.SqlQueryRaw<int>(
                    """
                    UPDATE dbo.InvoiceSequence WITH (UPDLOCK, ROWLOCK)
                    SET NextNumber = NextNumber + 1
                    OUTPUT deleted.NextNumber AS [Value]
                    WHERE Id = 1;
                    """)
                .ToListAsync(ct);

            var number = assigned.FirstOrDefault();
            if (number <= 0)
                throw new InvalidOperationException("No se pudo asignar el número de factura. Ejecuta 011_VehicleInvoiceAndPayment.sql.");

            vehicle.InvoiceNumber = number;
            vehicle.InvoiceIssuedAtUtc = DateTime.UtcNow;
            vehicle.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return number;
        });
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
