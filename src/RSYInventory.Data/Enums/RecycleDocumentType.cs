namespace RSYInventory.Data.Enums;

/// <summary>Document types for a recycle scrap load (all share the same load ID).</summary>
public enum RecycleDocumentType : byte
{
    Bol = 1,
    Nucor = 2,
    Scale = 3
}
