namespace RSYInventory.Data.Services;

/// <summary>
/// Model-year options for typeahead selectors (newest first).
/// </summary>
public static class VehicleYearCatalog
{
    public const int MinYear = 1950;

    /// <summary>Oldest year shown when opening the year dropdown without typing.</summary>
    public const int DropdownFloorYear = 1980;

    public static int MaxYear => DateTime.Today.Year + 1;

    /// <summary>Suggestion count so the open list scrolls at least down to <see cref="DropdownFloorYear"/>.</summary>
    public static int DropdownVisibleCount => Math.Max(1, MaxYear - DropdownFloorYear + 1);

    public static IReadOnlyList<string> AllYears { get; } = BuildYears();

    private static IReadOnlyList<string> BuildYears()
    {
        var max = DateTime.Today.Year + 1;
        var years = new List<string>(max - MinYear + 1);
        for (var y = max; y >= MinYear; y--)
            years.Add(y.ToString());
        return years;
    }
}
