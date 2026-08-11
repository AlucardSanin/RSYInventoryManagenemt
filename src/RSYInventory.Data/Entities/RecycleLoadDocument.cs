using System;

namespace RSYInventory.Data.Entities;

public partial class RecycleLoadDocument
{
    public int Id { get; set; }

    public int RecycleLoadId { get; set; }

    /// <summary><see cref="Enums.RecycleDocumentType"/> as byte.</summary>
    public byte DocumentType { get; set; }

    public string RelativePath { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public virtual RecycleLoad RecycleLoad { get; set; } = null!;
}
