namespace RSYInventory.Data.Entities;

public partial class AuditEvent
{
    public long Id { get; set; }

    public string EventType { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public int? EntityId { get; set; }

    public int UserId { get; set; }

    public string Summary { get; set; } = null!;

    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
}
