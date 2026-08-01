namespace RSYInventory.Data.Services;

/// <summary>
/// Common vehicle makes/brands for typeahead, merged with values already stored in the DB.
/// </summary>
public static class VehicleMakeCatalog
{
    public static readonly IReadOnlyList<string> Common = new[]
    {
        "Acura", "Alfa Romeo", "Audi", "BMW", "Buick", "Cadillac", "Chevrolet", "Chrysler",
        "Dodge", "Fiat", "Ford", "GMC", "Honda", "Hyundai", "Infiniti", "Jaguar", "Jeep",
        "Kia", "Land Rover", "Lexus", "Lincoln", "Mazda", "Mercedes-Benz", "Mercury",
        "Mini", "Mitsubishi", "Nissan", "Pontiac", "Porsche", "Ram", "Saturn", "Subaru",
        "Suzuki", "Tesla", "Toyota", "Volkswagen", "Volvo"
    };

    public static List<string> Merge(IEnumerable<string?> existing)
    {
        return Common
            .Concat(existing.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()))
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Any(char.IsLower)).First())
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
