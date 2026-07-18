using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Configuration;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public class InventoryService
{
    private readonly YardInventoryDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public InventoryService(YardInventoryDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<InventoryItem>> GetAvailableAsync(CancellationToken ct = default)
    {
        return await _db.InventoryItems
            .AsNoTracking()
            .Include(i => i.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .Where(i => i.Status == InventoryItemStatus.Available)
            .OrderByDescending(i => i.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<InventoryItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.InventoryItems
            .Include(i => i.Pallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
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
        CancellationToken ct = default)
    {
        EnsureCanEdit();

        await EnsurePalletCanAcceptAsync(palletId, itemType, excludeItemId: null, ct);

        var item = new InventoryItem
        {
            ItemType = itemType,
            Status = InventoryItemStatus.Available,
            PartNumber = partNumber,
            Brand = brand,
            Model = model,
            Year = year,
            Description = description,
            Notes = notes,
            PalletId = palletId,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.InventoryItems.Add(item);
        await _db.SaveChangesAsync(ct);

        _db.InventoryMovements.Add(new InventoryMovement
        {
            MovementType = MovementType.Assigned,
            InventoryItemId = item.Id,
            ToPalletId = palletId,
            UserId = _currentUser.UserId,
            Notes = "Alta de inventario",
            MovedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return item;
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

        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        if (item.Status == InventoryItemStatus.Sold)
            throw new InvalidOperationException("No se puede editar una pieza vendida.");

        item.PartNumber = partNumber;
        item.Brand = brand;
        item.Model = model;
        item.Year = year;
        item.Description = description;
        item.Notes = notes;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Change location: user must choose Sold or Relocated.
    /// </summary>
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

        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        if (item.Status == InventoryItemStatus.Sold)
            throw new InvalidOperationException("La pieza ya está vendida.");

        var fromPalletId = item.PalletId;

        if (movementType == MovementType.Sold)
        {
            item.Status = InventoryItemStatus.Sold;
            item.PalletId = null;
            item.UpdatedAtUtc = DateTime.UtcNow;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                MovementType = MovementType.Sold,
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

            await EnsurePalletCanAcceptAsync(newPalletId.Value, item.ItemType, excludeItemId: item.Id, ct);

            item.PalletId = newPalletId;
            item.UpdatedAtUtc = DateTime.UtcNow;

            _db.InventoryMovements.Add(new InventoryMovement
            {
                MovementType = MovementType.Relocated,
                InventoryItemId = item.Id,
                FromPalletId = fromPalletId,
                ToPalletId = newPalletId,
                UserId = _currentUser.UserId,
                Notes = notes ?? "Reubicado",
                MovedAtUtc = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        EnsureCanEdit();

        var item = await _db.InventoryItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new InvalidOperationException("Pieza no encontrada.");

        var hasMovements = await _db.InventoryMovements.AnyAsync(m => m.InventoryItemId == id, ct);
        if (hasMovements)
            throw new InvalidOperationException("No se puede eliminar una pieza con historial; márquela como vendida.");

        _db.InventoryItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<InventoryMovement>> GetPalletMovementsAsync(int palletId, int take = 50, CancellationToken ct = default)
    {
        return await _db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.InventoryItem)
            .Include(m => m.Vehicle)
            .Where(m => m.FromPalletId == palletId || m.ToPalletId == palletId)
            .OrderByDescending(m => m.MovedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    private async Task EnsurePalletCanAcceptAsync(
        int palletId,
        InventoryItemType itemType,
        int? excludeItemId,
        CancellationToken ct)
    {
        var pallet = await _db.Pallets
            .Include(p => p.Row)!.ThenInclude(r => r!.Zone)
            .FirstOrDefaultAsync(p => p.Id == palletId && p.IsActive, ct)
            ?? throw new InvalidOperationException("Paleta no encontrada o inactiva.");

        if (pallet.Row.Zone.Purpose != ZonePurpose.Parts)
            throw new InvalidOperationException("Solo se pueden colocar motores/transmisiones en zonas de partes.");

        var query = _db.InventoryItems.Where(i =>
            i.PalletId == palletId &&
            i.Status == InventoryItemStatus.Available);

        if (excludeItemId is not null)
            query = query.Where(i => i.Id != excludeItemId);

        var engines = await query.CountAsync(i => i.ItemType == InventoryItemType.Engine, ct);
        var transmissions = await query.CountAsync(i => i.ItemType == InventoryItemType.Transmission, ct);

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
