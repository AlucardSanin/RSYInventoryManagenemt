using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Services;

/// <summary>
/// Records and reads purchase-price change history for vehicles and pickup schedules.
/// </summary>
public sealed class VehiclePriceHistoryService(
    IDbContextFactory<YardInventoryDbContext> dbFactory,
    ICurrentUserService currentUser,
    AuditService audit)
{
    public static decimal? Normalize(decimal? price)
        => price is null or <= 0
            ? null
            : Math.Round(price.Value, 2, MidpointRounding.AwayFromZero);

    public static bool HasChanged(decimal? before, decimal? after)
        => Normalize(before) != Normalize(after);

    public void AddVehicleChange(
        YardInventoryDbContext db,
        int vehicleId,
        decimal? oldPrice,
        decimal? newPrice,
        string? notes = null)
    {
        var oldN = Normalize(oldPrice);
        var newN = Normalize(newPrice);
        if (oldN == newN)
            return;

        var userId = currentUser.UserId;
        if (userId <= 0)
            return;

        db.VehiclePriceHistories.Add(new VehiclePriceHistory
        {
            VehicleId = vehicleId,
            OldPrice = oldN,
            NewPrice = newN,
            ChangedByUserId = userId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ChangedAtUtc = DateTime.UtcNow
        });
    }

    public void AddScheduleChange(
        YardInventoryDbContext db,
        int scheduledPickupId,
        decimal? oldPrice,
        decimal? newPrice,
        string? notes = null)
    {
        var oldN = Normalize(oldPrice);
        var newN = Normalize(newPrice);
        if (oldN == newN)
            return;

        var userId = currentUser.UserId;
        if (userId <= 0)
            return;

        db.VehiclePriceHistories.Add(new VehiclePriceHistory
        {
            ScheduledVehiclePickupId = scheduledPickupId,
            OldPrice = oldN,
            NewPrice = newN,
            ChangedByUserId = userId,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            ChangedAtUtc = DateTime.UtcNow
        });
    }

    public Task WriteAuditForVehicleAsync(
        int vehicleId,
        decimal? oldPrice,
        decimal? newPrice,
        CancellationToken ct = default)
    {
        var oldN = Normalize(oldPrice);
        var newN = Normalize(newPrice);
        if (oldN == newN)
            return Task.CompletedTask;

        return audit.WriteAsync(
            "VehiclePriceChanged",
            "Vehicle",
            vehicleId,
            $"Precio {Format(oldN)} → {Format(newN)}",
            null,
            ct);
    }

    public Task WriteAuditForScheduleAsync(
        int scheduledPickupId,
        decimal? oldPrice,
        decimal? newPrice,
        CancellationToken ct = default)
    {
        var oldN = Normalize(oldPrice);
        var newN = Normalize(newPrice);
        if (oldN == newN)
            return Task.CompletedTask;

        return audit.WriteAsync(
            "VehiclePriceChanged",
            "ScheduledVehiclePickup",
            scheduledPickupId,
            $"Precio agenda {Format(oldN)} → {Format(newN)}",
            null,
            ct);
    }

    public async Task<IReadOnlyList<VehiclePriceHistory>> GetForVehicleAsync(
        int vehicleId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.VehiclePriceHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ThenByDescending(h => h.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VehiclePriceHistory>> GetForScheduleAsync(
        int scheduledPickupId,
        CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.VehiclePriceHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
            .Where(h => h.ScheduledVehiclePickupId == scheduledPickupId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ThenByDescending(h => h.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// When a scheduled pickup becomes an acquired vehicle, copy its price history onto the vehicle.
    /// </summary>
    public async Task CopyScheduleHistoryToVehicleAsync(
        YardInventoryDbContext db,
        int scheduledPickupId,
        int vehicleId,
        CancellationToken ct = default)
    {
        var rows = await db.VehiclePriceHistories
            .Where(h => h.ScheduledVehiclePickupId == scheduledPickupId)
            .OrderBy(h => h.ChangedAtUtc)
            .ThenBy(h => h.Id)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return;

        foreach (var row in rows)
        {
            db.VehiclePriceHistories.Add(new VehiclePriceHistory
            {
                VehicleId = vehicleId,
                OldPrice = row.OldPrice,
                NewPrice = row.NewPrice,
                ChangedByUserId = row.ChangedByUserId,
                Notes = string.IsNullOrWhiteSpace(row.Notes)
                    ? $"Desde agenda #{scheduledPickupId}"
                    : $"{row.Notes} (agenda #{scheduledPickupId})",
                ChangedAtUtc = row.ChangedAtUtc
            });
        }
    }

    private static string Format(decimal? value)
        => value is null ? "—" : value.Value.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
}
