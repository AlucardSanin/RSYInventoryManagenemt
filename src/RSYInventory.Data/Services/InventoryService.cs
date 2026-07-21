using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Configuration;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public class InventoryService
{
    private readonly IDbContextFactory<YardInventoryDbContext> _dbFactory;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(IDbContextFactory<YardInventoryDbContext> dbFactory, ICurrentUserService currentUser)
    {
        _dbFactory = dbFactory;
        _currentUser = currentUser;
    }

    public async Task<List<InventoryItem>> GetAvailableAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var available = (int)InventoryItemStatus.Available;
        return await db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .Where(i => i.Status == available)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InventoryItems
            .Include(i => i.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<List<string>> GetDistinctBrandsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var stored = await db.InventoryItems
            .AsNoTracking()
            .Where(i => i.Brand != null && i.Brand != "")
            .Select(i => i.Brand!)
            .Distinct()
            .ToListAsync(ct);
        return VehicleMakeCatalog.Merge(stored);
    }

    public async Task<List<string>> GetDistinctModelsAsync(string? brand = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var query = db.InventoryItems.AsNoTracking().Where(i => i.Model != null && i.Model != "");
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var b = brand.Trim();
            query = query.Where(i => i.Brand != null && i.Brand.ToLower() == b.ToLower());
        }

        return await query
            .Select(i => i.Model!)
            .Distinct()
            .OrderBy(m => m)
            .Take(200)
            .ToListAsync(ct);
    }

    public async Task<InventoryItem> AddAsync(
        InventoryItemType itemType,
        string? partNumber,
        string? brand,
        string? model,
        int? year,
        string? description,
        string? notes,
        int palletId,
        string? sourceVin = null,
        decimal? displacementLiters = null,
        TransmissionType? transmissionType = null,
        VehicleDriveType? driveType = null,
        CancellationToken ct = default)
    {
        EnsureCanEdit();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        await EnsurePalletCanAcceptAsync(db, palletId, itemType, excludeItemId: null, ct);

        if (itemType == InventoryItemType.Engine && displacementLiters is null)
            throw new InvalidOperationException("Indique la cilindrada del motor (litros).");

        if (itemType == InventoryItemType.Transmission)
        {
            if (transmissionType is null)
                throw new InvalidOperationException("Seleccione el tipo de transmisión (automática o manual).");
            if (driveType is null)
                throw new InvalidOperationException("Seleccione el tipo de tracción (FWD, RWD, AWD, 4x4…).");
        }

        var normalizedVin = VinDecoder.Normalize(sourceVin);

        var item = new InventoryItem
        {
            ItemType = (int)itemType,
            Status = (int)InventoryItemStatus.Available,
            PartNumber = partNumber,
            Brand = brand,
            Model = model,
            Year = year,
            Description = description,
            Notes = notes,
            SourceVin = string.IsNullOrEmpty(normalizedVin) ? null : normalizedVin,
            DisplacementLiters = itemType == InventoryItemType.Engine ? displacementLiters : null,
            TransmissionType = itemType == InventoryItemType.Transmission ? (int?)transmissionType : null,
            DriveType = itemType == InventoryItemType.Transmission ? (int?)driveType : null,
            PalletId = palletId,
            CreatedByUserId = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.InventoryItems.Add(item);
        await db.SaveChangesAsync(ct);

        db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = (int)MovementType.Assigned,
            InventoryItemId = item.Id,
            ToPalletId = palletId,
            UserId = _currentUser.UserId,
            Notes = "Alta de inventario",
            MovedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return item;
    }

    public async Task SetImagePathAsync(int itemId, string? relativePath, CancellationToken ct = default)
    {
        EnsureCanEdit();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");
        item.ImageRelativePath = relativePath;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateDetailsAsync(
        int id,
        string? partNumber,
        string? brand,
        string? model,
        int? year,
        string? description,
        string? notes,
        CancellationToken ct = default)
    {
        EnsureCanEdit();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        if (item.Status == (int)InventoryItemStatus.Sold)
            throw new InvalidOperationException("No se puede editar una pieza vendida.");

        item.PartNumber = partNumber;
        item.Brand = brand;
        item.Model = model;
        item.Year = year;
        item.Description = description;
        item.Notes = notes;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public async Task ChangeLocationAsync(
        int itemId,
        MovementType movementType,
        int? newPalletId,
        string? notes,
        CancellationToken ct = default)
    {
        EnsureCanEdit();

        if (!MovementRules.RequiresSoldOrRelocateChoice(movementType))
            throw new InvalidOperationException("Debe indicar si fue Vendido o solo Reubicado.");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        if (item.Status == (int)InventoryItemStatus.Sold)
            throw new InvalidOperationException("La pieza ya está vendida.");

        var fromPalletId = item.PalletId;

        if (movementType == MovementType.Sold)
        {
            item.Status = (int)InventoryItemStatus.Sold;
            item.PalletId = null;
            item.UpdatedAtUtc = DateTime.UtcNow;
            item.DeletedAtUtc = DateTime.UtcNow;

            db.InventoryMovements.Add(new InventoryMovement
            {
                MovementType = (int)MovementType.Sold,
                InventoryItemId = item.Id,
                FromPalletId = fromPalletId,
                ToPalletId = null,
                UserId = _currentUser.UserId,
                Notes = notes ?? "Vendido",
                MovedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            if (newPalletId is null)
                throw new InvalidOperationException("Seleccione la paleta de destino para reubicar.");

            if (newPalletId == fromPalletId)
                throw new InvalidOperationException("La paleta de destino es la misma.");

            await EnsurePalletCanAcceptAsync(db, newPalletId.Value, (InventoryItemType)item.ItemType, excludeItemId: item.Id, ct);

            item.PalletId = newPalletId;
            item.UpdatedAtUtc = DateTime.UtcNow;

            db.InventoryMovements.Add(new InventoryMovement
            {
                MovementType = (int)MovementType.Relocated,
                InventoryItemId = item.Id,
                FromPalletId = fromPalletId,
                ToPalletId = newPalletId,
                UserId = _currentUser.UserId,
                Notes = notes ?? "Reubicado",
                MovedAtUtc = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        EnsureCanEdit();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        var hasMovements = await db.InventoryMovements.AnyAsync(m => m.InventoryItemId == id, ct);
        if (hasMovements)
            throw new InvalidOperationException("No se puede eliminar una pieza con historial; márquela como vendida.");

        db.InventoryItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<InventoryMovement>> GetPalletMovementsAsync(int palletId, int take = 50, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.InventoryItem)
            .Include(m => m.Vehicle)
            .Where(m => m.FromPalletId == palletId || m.ToPalletId == palletId)
            .OrderByDescending(m => m.MovedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    private static async Task EnsurePalletCanAcceptAsync(
        YardInventoryDbContext db,
        int palletId,
        InventoryItemType itemType,
        int? excludeItemId,
        CancellationToken ct)
    {
        var pallet = await db.Pallets
            .Include(p => p.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(p => p.Id == palletId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Paleta no encontrada o inactiva.");

        if (pallet.Row.Zone.Purpose != (int)ZonePurpose.Parts)
            throw new InvalidOperationException("Solo se pueden colocar motores/transmisiones en zonas de partes.");

        var available = (int)InventoryItemStatus.Available;
        var query = db.InventoryItems.Where(i =>
            i.PalletId == palletId &&
            i.Status == available);

        if (excludeItemId is not null)
            query = query.Where(i => i.Id != excludeItemId);

        var engine = (int)InventoryItemType.Engine;
        var transmission = (int)InventoryItemType.Transmission;
        var engines = await query.CountAsync(i => i.ItemType == engine, ct);
        var transmissions = await query.CountAsync(i => i.ItemType == transmission, ct);

        if (!PalletCapacityRules.CanPlace(itemType, engines, transmissions))
        {
            throw new InvalidOperationException(
                itemType == InventoryItemType.Engine
                    ? "La paleta ya tiene un motor (máximo 1)."
                    : "La paleta ya tiene 2 transmisiones (máximo 2).");
        }
    }

    private void EnsureCanEdit()
    {
        if (!_currentUser.CanEditInventory && !_currentUser.CanManageZones)
            throw new UnauthorizedAccessException("No tiene permiso para editar inventario.");
    }
}
