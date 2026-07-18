namespace RSYInventory.Data.Entities;

public class Row
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;

    /// <summary>1-based row number within the zone.</summary>
    public int RowNumber { get; set; }
    public string? Label { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Pallet> Pallets { get; set; } = new List<Pallet>();
}
