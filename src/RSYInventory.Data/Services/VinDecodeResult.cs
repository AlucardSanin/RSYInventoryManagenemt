using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Fields suggested from a VIN (NHTSA vPIC when available, local fallback otherwise).
/// </summary>
public sealed class VinDecodeResult
{
    public required string Vin { get; init; }
    public int? Year { get; init; }
    public string? Make { get; init; }
    public string? Model { get; init; }
    public decimal? DisplacementLiters { get; init; }
    public TransmissionType? TransmissionType { get; init; }
    public VehicleDriveType? DriveType { get; init; }
    public string? BodyClass { get; init; }
    public string? Trim { get; init; }
    public string? Series { get; init; }
    public int? EngineCylinders { get; init; }
    public string? Manufacturer { get; init; }
    public bool FromNhtsa { get; init; }

    /// <summary>Hard failure (invalid VIN, total decode failure). Show as error in UI.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Soft notice (API down, timeout). Show alongside any partial local data.</summary>
    public string? Warning { get; init; }

    public bool HasAnyData =>
        Year is not null
        || !string.IsNullOrWhiteSpace(Make)
        || !string.IsNullOrWhiteSpace(Model)
        || DisplacementLiters is not null
        || TransmissionType is not null
        || DriveType is not null;

    /// <summary>Short label useful as a part description seed.</summary>
    public string? SuggestedDescription
    {
        get
        {
            try
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Trim))
                    parts.Add(Trim.Trim());
                if (!string.IsNullOrWhiteSpace(Series) && !string.Equals(Series, Trim, StringComparison.OrdinalIgnoreCase))
                    parts.Add(Series.Trim());
                if (!string.IsNullOrWhiteSpace(BodyClass))
                    parts.Add(BodyClass.Trim());
                if (EngineCylinders is > 0 && DisplacementLiters is > 0)
                    parts.Add($"V{EngineCylinders} {DisplacementLiters:0.#}L");
                else if (DisplacementLiters is > 0)
                    parts.Add($"{DisplacementLiters:0.#}L");
                else if (EngineCylinders is > 0)
                    parts.Add($"{EngineCylinders} cyl");

                return parts.Count == 0 ? null : string.Join(" · ", parts);
            }
            catch
            {
                return null;
            }
        }
    }
}
