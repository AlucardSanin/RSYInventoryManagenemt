using Microsoft.EntityFrameworkCore;
using RSYInventory.Data.Data;
using RSYInventory.Data.Entities;

namespace RSYInventory.Data.Services;

public sealed class AuditService(
    IDbContextFactory<YardInventoryDbContext> dbFactory,
    ICurrentUserService currentUser)
{
    public async Task WriteAsync(
        string eventType,
        string entityType,
        int? entityId,
        string summary,
        string? details = null,
        CancellationToken ct = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId <= 0)
            return;

        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = eventType,
                EntityType = entityType,
                EntityId = entityId,
                UserId = currentUser.UserId,
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
