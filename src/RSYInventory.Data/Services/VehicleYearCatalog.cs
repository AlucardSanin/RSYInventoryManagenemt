namespace RSYInventory.Data.Services;

/// <summary>
/// Model-year options for typeahead selectors (newest first).
/// </summary>
public static class VehicleYearCatalog
{
    public const int MinYear = 1950;

    public static int MaxYear => DateTime.Today.Year + 1;

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
