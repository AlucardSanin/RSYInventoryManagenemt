using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Configuration;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public class YardLayoutService
{
    private readonly YardInventoryDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public YardLayoutService(YardInventoryDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<Zone>> GetZonesAsync(ZonePurpose? purpose = null, CancellationToken ct = default)
    {
        var query = _db.Zones
            .AsNoTracking()
            .Include(z => z.Rows)
                .ThenInclude(r => r.Pallets)
            .Where(z => z.IsActive);

        if (purpose is not null)
            query = query.Where(z => z.Purpose == purpose);

        return await query
            .OrderBy(z => z.Purpose)
            .ThenBy(z => z.Name)
            .ToListAsync(ct);
    }

    public async Task<List<Pallet>> GetPalletsAsync(ZonePurpose purpose, CancellationToken ct = default)
    {
        return await _db.Pallets
            .AsNoTracking()
            .Include(p => p.Row)!.ThenInclude(r => r!.Zone)
            .Where(p => p.IsActive && p.Row.IsActive && p.Row.Zone.IsActive && p.Row.Zone.Purpose == purpose)
            .OrderBy(p => p.Row.Zone.Name)
            .ThenBy(p => p.Row.RowNumber)
            .ThenBy(p => p.PalletNumber)
            .ToListAsync(ct);
    }

    public static string FormatPalletLabel(Pallet pallet)
        => $"{pallet.Row.Zone.Name} / Fila {pallet.Row.RowNumber} / Paleta {pallet.PalletNumber}"
           + (string.IsNullOrWhiteSpace(pallet.Label) ? string.Empty : $" ({pallet.Label})");

    public async Task<Zone> CreateZoneAsync(
        string name,
        ZonePurpose purpose,
        int rowCount,
        int palletsPerRow,
        CancellationToken ct = default)
    {
        EnsureCanManageZones();

        if (rowCount < 1 || palletsPerRow < 1)
            throw new InvalidOperationException("La zona debe tener al menos 1 fila y 1 paleta por fila.");

        if (await _db.Zones.AnyAsync(z => z.Name == name && z.Purpose == purpose, ct))
            throw new InvalidOperationException("Ya existe una zona con ese nombre y propósito.");

        var zone = new Zone
        {
            Name = name.Trim(),
            Purpose = purpose,
            RowCount = rowCount,
            PalletsPerRow = palletsPerRow,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        for (var r = 1; r <= rowCount; r++)
        {
            var row = new Row
            {
                RowNumber = r,
                Label = $"{zone.Name}-R{r}",
                IsActive = true
            };

            for (var p = 1; p <= palletsPerRow; p++)
            {
                row.Pallets.Add(new Pallet
                {
                    PalletNumber = p,
                    Label = $"{zone.Name}-R{r}-P{p}",
                    IsActive = true
                });
            }

            zone.Rows.Add(row);
        }

        _db.Zones.Add(zone);
        await _db.SaveChangesAsync(ct);
        return zone;
    }

    public async Task ResizeZoneAsync(int zoneId, int newRowCount, int newPalletsPerRow, CancellationToken ct = default)
    {
        EnsureCanManageZones();

        var zone = await _db.Zones
            .Include(z => z.Rows)
                .ThenInclude(r => r.Pallets)
                    .ThenInclude(p => p.InventoryItems)
            .Include(z => z.Rows)
                .ThenInclude(r => r.Pallets)
                    .ThenInclude(p => p.Vehicles)
            .FirstOrDefaultAsync(z => z.Id == zoneId, ct)
            ?? throw new InvalidOperationException("Zona no encontrada.");

        if (newRowCount < 1 || newPalletsPerRow < 1)
            throw new InvalidOperationException("La zona debe tener al menos 1 fila y 1 paleta por fila.");

        // Shrink: only remove empty trailing rows/pallets
        var rowsOrdered = zone.Rows.OrderBy(r => r.RowNumber).ToList();
        if (newRowCount < rowsOrdered.Count)
        {
            var toRemove = rowsOrdered.Skip(newRowCount).ToList();
            foreach (var row in toRemove)
            {
                var inv = row.Pallets.SelectMany(p => p.InventoryItems.Where(i => i.Status == InventoryItemStatus.Available)).Count();
                var veh = row.Pallets.SelectMany(p => p.Vehicles).Count();
                if (!YardLayoutRules.CanDeleteLocation(inv, veh))
                    throw new InvalidOperationException(YardLayoutRules.DeletionBlockedMessage + $" (Fila {row.RowNumber})");
            }

            _db.Rows.RemoveRange(toRemove);
        }

        foreach (var row in zone.Rows.Where(r => r.RowNumber <= newRowCount).OrderBy(r => r.RowNumber))
        {
            var palletsOrdered = row.Pallets.OrderBy(p => p.PalletNumber).ToList();
            if (newPalletsPerRow < palletsOrdered.Count)
            {
                var toRemove = palletsOrdered.Skip(newPalletsPerRow).ToList();
                foreach (var pallet in toRemove)
                {
                    var inv = pallet.InventoryItems.Count(i => i.Status == InventoryItemStatus.Available);
                    var veh = pallet.Vehicles.Count;
                    if (!YardLayoutRules.CanDeleteLocation(inv, veh))
                        throw new InvalidOperationException(YardLayoutRules.DeletionBlockedMessage + $" (Paleta {pallet.PalletNumber})");
                }

                _db.Pallets.RemoveRange(toRemove);
            }

            for (var p = palletsOrdered.Count + 1; p <= newPalletsPerRow; p++)
            {
                row.Pallets.Add(new Pallet
                {
                    PalletNumber = p,
                    Label = $"{zone.Name}-R{row.RowNumber}-P{p}",
                    IsActive = true
                });
            }
        }

        for (var r = rowsOrdered.Count + 1; r <= newRowCount; r++)
        {
            var row = new Row
            {
                ZoneId = zone.Id,
                RowNumber = r,
                Label = $"{zone.Name}-R{r}",
                IsActive = true
            };
            for (var p = 1; p <= newPalletsPerRow; p++)
            {
                row.Pallets.Add(new Pallet
                {
                    PalletNumber = p,
                    Label = $"{zone.Name}-R{r}-P{p}",
                    IsActive = true
                });
            }

            _db.Rows.Add(row);
        }

        zone.RowCount = newRowCount;
        zone.PalletsPerRow = newPalletsPerRow;
        zone.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteZoneAsync(int zoneId, CancellationToken ct = default)
    {
        EnsureCanManageZones();

        var zone = await _db.Zones
            .Include(z => z.Rows)
                .ThenInclude(r => r.Pallets)
                    .ThenInclude(p => p.InventoryItems)
            .Include(z => z.Rows)
                .ThenInclude(r => r.Pallets)
                    .ThenInclude(p => p.Vehicles)
            .FirstOrDefaultAsync(z => z.Id == zoneId, ct)
            ?? throw new InvalidOperationException("Zona no encontrada.");

        var inv = zone.Rows.SelectMany(r => r.Pallets).SelectMany(p => p.InventoryItems)
            .Count(i => i.Status == InventoryItemStatus.Available);
        var veh = zone.Rows.SelectMany(r => r.Pallets).SelectMany(p => p.Vehicles).Count();

        if (!YardLayoutRules.CanDeleteLocation(inv, veh))
            throw new InvalidOperationException(YardLayoutRules.DeletionBlockedMessage);

        foreach (var row in zone.Rows.ToList())
        {
            _db.Pallets.RemoveRange(row.Pallets);
            _db.Rows.Remove(row);
        }

        _db.Zones.Remove(zone);
        await _db.SaveChangesAsync(ct);
    }

    private void EnsureCanManageZones()
    {
        if (!_currentUser.CanManageZones)
            throw new UnauthorizedAccessException("No tiene permiso para gestionar zonas.");
    }
}
