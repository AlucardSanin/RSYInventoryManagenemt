using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Entities;

/// <summary>
/// Yard zone. Rows and pallets are flexible: configured via RowCount and PalletsPerRow
/// and materialized as Row/Pallet children when a ZoneManager creates or edits the zone.
/// </summary>
public class Zone
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ZonePurpose Purpose { get; set; }
    public int RowCount { get; set; }
    public int PalletsPerRow { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Row> Rows { get; set; } = new List<Row>();
}
