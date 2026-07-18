using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public class VehicleService
{
    private readonly YardInventoryDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VehicleService(YardInventoryDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<VehicleSource>> GetSourcesAsync(CancellationToken ct = default)
    {
        return await _db.VehicleSources
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);
    }

    public async Task<List<Vehicle>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleSource)
            .Include(v => v.AcquiredByUser)
            .Include(v => v.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .OrderByDescending(v => v.AcquiredAt)
            .ToListAsync(ct);
    }

    public async Task<Vehicle?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Vehicles
            .Include(v => v.VehicleSource)
            .Include(v => v.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
    }

    /// <summary>
    /// Registers an acquired vehicle. Does not assign yard location.
    /// </summary>
    public async Task<Vehicle> AcquireAsync(
        string vin,
        int? year,
        string? make,
        string? model,
        TransmissionType? transmissionType,
        VehicleDriveType? driveType,
        int? mileage,
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

        if (await _db.Vehicles.AnyAsync(v => v.Vin == vin, ct))
            throw new InvalidOperationException("Ya existe un vehículo con ese VIN.");

        if (!await _db.VehicleSources.AnyAsync(s => s.Id == vehicleSourceId && s.IsActive, ct))
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
            Observations = observations,
            VehicleSourceId = vehicleSourceId,
            AcquiredAt = acquiredAt,
            AcquiredByUserId = _currentUser.UserId,
            PalletId = null,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync(ct);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = (int)MovementType.Acquired,
            VehicleId = vehicle.Id,
            UserId = _currentUser.UserId,
            Notes = $"Adquirido desde fuente #{vehicleSourceId}",
            MovedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return vehicle;
    }

    /// <summary>
    /// Inventory staff assigns a vehicle to a vehicle-zone pallet.
    /// </summary>
    public async Task AssignLocationAsync(int vehicleId, int palletId, string? notes, CancellationToken ct = default)
    {
        if (!_currentUser.CanEditInventory && !_currentUser.CanManageZones)
            throw new UnauthorizedAccessException("No tiene permiso para ubicar vehículos.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct)
            ?? throw new InvalidOperationException("Vehículo no encontrado.");

        var pallet = await _db.Pallets
            .Include(p => p.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(p => p.Id == palletId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Paleta no encontrada o inactiva.");

        if (pallet.Row.Zone.Purpose != (int)ZonePurpose.Vehicles)
            throw new InvalidOperationException("Solo se pueden ubicar vehículos en zonas de vehículos.");

        var fromPalletId = vehicle.PalletId;
        vehicle.PalletId = palletId;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;

        _db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = (int)(fromPalletId is null ? MovementType.Assigned : MovementType.Relocated),
            VehicleId = vehicle.Id,
            FromPalletId = fromPalletId,
            ToPalletId = palletId,
            UserId = _currentUser.UserId,
            Notes = notes ?? (fromPalletId is null ? "Ubicación asignada" : "Vehículo reubicado"),
            MovedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Best-effort VIN decode for year/make placeholders (WMI table can grow later).
    /// </summary>
    public static (int? Year, string? Make) TryDecodeVin(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Trim().Length < 10)
            return (null, null);

        vin = vin.Trim().ToUpperInvariant();
        var year = DecodeModelYear(vin[9]);
        var make = DecodeMake(vin[..3]);
        return (year, make);
    }

    private static int? DecodeModelYear(char code) => code switch
    {
        'A' => 2010, 'B' => 2011, 'C' => 2012, 'D' => 2013, 'E' => 2014,
        'F' => 2015, 'G' => 2016, 'H' => 2017, 'J' => 2018, 'K' => 2019,
        'L' => 2020, 'M' => 2021, 'N' => 2022, 'P' => 2023, 'R' => 2024,
        'S' => 2025, 'T' => 2026, 'V' => 2027, 'W' => 2028, 'X' => 2029,
        'Y' => 2030,
        '1' => 2001, '2' => 2002, '3' => 2003, '4' => 2004, '5' => 2005,
        '6' => 2006, '7' => 2007, '8' => 2008, '9' => 2009,
        _ => null
    };

    private static string? DecodeMake(string wmi) => wmi switch
    {
        "1G1" or "1G6" or "1GC" => "Chevrolet",
        "1FA" or "1FT" or "1FMCU" => "Ford",
        "1J4" or "1C4" => "Jeep",
        "2T1" or "4T1" or "5TD" => "Toyota",
        "3VW" or "1VW" => "Volkswagen",
        "5YJ" => "Tesla",
        "JM1" or "3MZ" => "Mazda",
        "KNA" or "5XY" => "Kia",
        "5NP" or "KM8" => "Hyundai",
        "1N4" or "3N1" => "Nissan",
        "WBA" or "WBS" => "BMW",
        "WDB" or "WDD" => "Mercedes-Benz",
        "JH4" or "19U" => "Acura",
        "JHM" or "1HG" => "Honda",
        _ => null
    };
}
