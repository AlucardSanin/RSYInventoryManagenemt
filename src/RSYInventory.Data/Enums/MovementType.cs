namespace RSYInventory.Data.Enums;

/// <summary>
/// When changing an item location, the user must choose Sold or Relocated.
/// </summary>
public enum MovementType
{
    Relocated = 1,
    Sold = 2,
    Assigned = 3,
    Acquired = 4
}
