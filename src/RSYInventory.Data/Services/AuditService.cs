using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Services;

public sealed class AuditService(
    IDbContextFactory<YardInventoryDbContext> dbFactory,
    ICurrentUserService currentUser)
{
    public Task WriteAsync(
        string eventType,
        string entityType,
        int? entityId,
        string summary,
        string? details = null,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId <= 0)
            return Task.CompletedTask;

        return WriteForUserAsync(
            currentUser.UserId,
            eventType,
            entityType,
            entityId,
            summary,
            details,
            ct);
    }

    /// <summary>Write an audit row as a specific user (e.g. driver portal actions).</summary>
    public async Task WriteForUserAsync(
        int userId,
        string eventType,
        string entityType,
        int? entityId,
        string summary,
        string? details = null,
        CancellationToken ct = default)
    {
        if (userId <= 0)
            return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                UserId = userId,
                Summary = summary.Length > 500 ? summary[..500] : summary,
                Details = details is { Length: > 2000 } ? details[..2000] : details,
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Audit must never break the primary operation.
        }
    }
}
