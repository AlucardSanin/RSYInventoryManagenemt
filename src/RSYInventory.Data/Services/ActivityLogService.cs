using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class ActivityLogEntry
{
    public DateTime AtUtc { get; init; }
    public string Source { get; init; } = "";
    public string EventType { get; init; } = "";
    public string Summary { get; init; } = "";
    public string? Details { get; init; }
    public string UserName { get; init; } = "";
    public string? Location { get; init; }
}

/// <summary>Admin-only searchable activity feed (movements + audit events).</summary>
public sealed class ActivityLogService(
    IDbContextFactory<YardInventoryDbContext> dbFactory,
    ICurrentUserService currentUser)
{
    public async Task<List<ActivityLogEntry>> SearchAsync(
        string? query = null,
        string? userName = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int take = 200,
        CancellationToken ct = default)
    {
        if (!currentUser.CanManageUsers)
            throw new UnauthorizedAccessException("Solo el administrador puede ver el log de actividad.");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var q = (query ?? string.Empty).Trim();
        var userFilter = (userName ?? string.Empty).Trim();
        take = Math.Clamp(take, 20, 500);

        var movementsQuery = db.InventoryMovements
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.FromPallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .Include(m => m.ToPallet)!.ThenInclude(p => p!.Row)!.ThenInclude(r => r!.Zone)
            .Include(m => m.InventoryItem)
            .Include(m => m.Vehicle)
            .AsQueryable();

        if (fromUtc is not null)
            movementsQuery = movementsQuery.Where(m => m.MovedAtUtc >= fromUtc);
        if (toUtc is not null)
            movementsQuery = movementsQuery.Where(m => m.MovedAtUtc <= toUtc);
        if (!string.IsNullOrEmpty(userFilter))
            movementsQuery = movementsQuery.Where(m =>
                m.User.UserName.Contains(userFilter) || m.User.DisplayName.Contains(userFilter));

        var movements = await movementsQuery
            .OrderByDescending(m => m.MovedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var auditQuery = db.AuditEvents
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (fromUtc is not null)
            auditQuery = auditQuery.Where(a => a.CreatedAtUtc >= fromUtc);
        if (toUtc is not null)
            auditQuery = auditQuery.Where(a => a.CreatedAtUtc <= toUtc);
        if (!string.IsNullOrEmpty(userFilter))
            auditQuery = auditQuery.Where(a =>
                a.User.UserName.Contains(userFilter) || a.User.DisplayName.Contains(userFilter));

        var audits = await auditQuery
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);

        var entries = new List<ActivityLogEntry>(movements.Count + audits.Count);

        foreach (var m in movements)
        {
            var type = ((MovementType)m.MovementType).ToString();
            var subject = m.InventoryItem is not null
                ? $"Pieza #{m.InventoryItemId} {m.InventoryItem.Brand} {m.InventoryItem.Model}".Trim()
                : m.Vehicle is not null
                    ? $"Vehículo {m.Vehicle.Vin} {m.Vehicle.Make} {m.Vehicle.Model}".Trim()
                    : "Movimiento";

            var from = m.FromPallet is null ? null : YardLayoutService.FormatPalletLabel(m.FromPallet);
            var to = m.ToPallet is null ? null : YardLayoutService.FormatPalletLabel(m.ToPallet);
            var location = from is null && to is null ? null : $"{from ?? "—"} → {to ?? "—"}";

            entries.Add(new ActivityLogEntry
            {
                AtUtc = m.MovedAtUtc,
                Source = "Movimiento",
                EventType = type,
                Summary = $"{type}: {subject}",
                Details = m.Notes,
                UserName = m.User.DisplayName,
                Location = location
            });
        }

        foreach (var a in audits)
        {
            entries.Add(new ActivityLogEntry
            {
                AtUtc = a.CreatedAtUtc,
                Source = "Auditoría",
                EventType = a.EventType,
                Summary = a.Summary,
                Details = a.Details,
                UserName = a.User.DisplayName,
                Location = $"{a.EntityType}{(a.EntityId is null ? "" : $" #{a.EntityId}")}"
            });
        }

        IEnumerable<ActivityLogEntry> result = entries.OrderByDescending(e => e.AtUtc);

        if (!string.IsNullOrEmpty(q))
        {
            result = result.Where(e =>
                e.Summary.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (e.Details?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || e.UserName.Contains(q, StringComparison.OrdinalIgnoreCase)
                || e.EventType.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (e.Location?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || e.Source.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        return result.Take(take).ToList();
    }
}
