using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

/// <summary>
/// Decodes VINs via NHTSA vPIC, with a local year/make fallback when the API is unavailable.
/// Never throws for network/API/parse failures — always returns a result the UI can show.
/// </summary>
public sealed class VinLookupService(HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<string, IReadOnlyList<string>> ModelsByMakeCache =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<VinDecodeResult> DecodeAsync(string? vin, CancellationToken cancellationToken = default)
    {
        string normalized;
        try
        {
            normalized = VinDecoder.Normalize(vin);
        }
        catch
        {
            return Fail(string.Empty, "No se pudo leer el VIN.");
        }

        if (normalized.Length is < 11 or > 17)
            return Fail(normalized, "El VIN debe tener entre 11 y 17 caracteres.");

        try
        {
            var fromApi = await TryDecodeFromNhtsaAsync(normalized, cancellationToken).ConfigureAwait(false);
            if (fromApi is not null)
                return fromApi;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Real caller cancellation — still prefer a soft result over breaking the Blazor circuit.
            return DecodeLocal(
                normalized,
                warning: "La consulta se canceló. Se usó decodificación local si estaba disponible.");
        }
        catch
        {
            // Timeout, DNS, TLS, HTTP, JSON, unexpected mapping — fall through to local.
        }

        return DecodeLocal(
            normalized,
            warning: "No se pudo obtener respuesta de NHTSA. Se usó decodificación local.");
    }

    /// <summary>
    /// Models for a make from NHTSA (cached). Never throws — returns empty on failure.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetModelsForMakeAsync(
        string? make,
        CancellationToken cancellationToken = default)
    {
        try
        {
            make = make?.Trim();
            if (string.IsNullOrWhiteSpace(make) || make.Length < 2)
                return [];

            if (ModelsByMakeCache.TryGetValue(make, out var cached))
                return cached;

            var url = $"api/vehicles/GetModelsForMake/{Uri.EscapeDataString(make)}?format=json";
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return [];

            NhtsaModelsResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<NhtsaModelsResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                return [];
            }

            var models = (payload?.Results ?? [])
                .Select(r => NullIfEmpty(r.Model_Name))
                .Where(m => m is not null)
                .Select(m => m!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ModelsByMakeCache[make] = models;
            return models;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return [];
        }
        catch
        {
            return [];
        }
    }

    private async Task<VinDecodeResult?> TryDecodeFromNhtsaAsync(string vin, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"api/vehicles/DecodeVinValues/{Uri.EscapeDataString(vin)}?format=json";
            using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return null;

            NhtsaDecodeResponse? payload;
            try
            {
                payload = await response.Content
                    .ReadFromJsonAsync<NhtsaDecodeResponse>(JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            var row = payload?.Results?.FirstOrDefault();
            if (row is null)
                return null;

            try
            {
                var mapped = MapNhtsa(vin, row);
                return mapped.HasAnyData ? mapped : null;
            }
            catch
            {
                return null;
            }
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient timeout (also surfaces as TaskCanceledException).
            return null;
        }
    }

    private static VinDecodeResult DecodeLocal(string vin, string? warning)
    {
        try
        {
            var (year, make) = VinDecoder.TryDecode(vin);
            return new VinDecodeResult
            {
                Vin = vin,
                Year = year,
                Make = make,
                FromNhtsa = false,
                Warning = warning
            };
        }
        catch
        {
            return Fail(vin, "No se pudo decodificar este VIN. Completa los campos manualmente.");
        }
    }

    private static VinDecodeResult Fail(string vin, string error) => new()
    {
        Vin = vin,
        FromNhtsa = false,
        ErrorMessage = error
    };

    private static VinDecodeResult MapNhtsa(string vin, NhtsaDecodeRow row)
    {
        int? year = null;
        if (int.TryParse(NullIfEmpty(row.ModelYear), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y)
            && y is >= 1980 and <= 2100)
        {
            year = y;
        }

        decimal? displacement = null;
        if (decimal.TryParse(NullIfEmpty(row.DisplacementL), NumberStyles.Number, CultureInfo.InvariantCulture, out var liters)
            && liters > 0)
        {
            displacement = Math.Round(liters, 1, MidpointRounding.AwayFromZero);
        }

        int? cylinders = null;
        if (int.TryParse(NullIfEmpty(row.EngineCylinders), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cyl)
            && cyl > 0)
        {
            cylinders = cyl;
        }

        return new VinDecodeResult
        {
            Vin = vin,
            Year = year,
            Make = TitleCaseMake(NullIfEmpty(row.Make)),
            Model = NullIfEmpty(row.Model),
            DisplacementLiters = displacement,
            TransmissionType = MapTransmission(row.TransmissionStyle),
            DriveType = MapDrive(row.DriveType),
            BodyClass = NullIfEmpty(row.BodyClass),
            Trim = NullIfEmpty(row.Trim),
            Series = NullIfEmpty(row.Series),
            EngineCylinders = cylinders,
            Manufacturer = NullIfEmpty(row.Manufacturer),
            FromNhtsa = true
        };
    }

    private static TransmissionType? MapTransmission(string? value)
    {
        try
        {
            var text = NullIfEmpty(value);
            if (text is null) return null;

            if (text.Contains("manual", StringComparison.OrdinalIgnoreCase))
                return TransmissionType.Manual;
            if (text.Contains("automatic", StringComparison.OrdinalIgnoreCase)
                || text.Contains("auto", StringComparison.OrdinalIgnoreCase)
                || text.Contains("cvt", StringComparison.OrdinalIgnoreCase))
                return TransmissionType.Automatic;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static VehicleDriveType? MapDrive(string? value)
    {
        try
        {
            var text = NullIfEmpty(value);
            if (text is null) return null;

            if (text.Contains("4x2", StringComparison.OrdinalIgnoreCase))
                return VehicleDriveType.FourByTwo;
            if (text.Contains("4x4", StringComparison.OrdinalIgnoreCase)
                || text.Contains("4wd", StringComparison.OrdinalIgnoreCase)
                || text.Contains("4-wheel", StringComparison.OrdinalIgnoreCase))
                return VehicleDriveType.FourByFour;
            if (text.Contains("awd", StringComparison.OrdinalIgnoreCase)
                || text.Contains("all-wheel", StringComparison.OrdinalIgnoreCase)
                || text.Contains("all wheel", StringComparison.OrdinalIgnoreCase))
                return VehicleDriveType.Awd;
            if (text.Contains("fwd", StringComparison.OrdinalIgnoreCase)
                || text.Contains("front-wheel", StringComparison.OrdinalIgnoreCase)
                || text.Contains("front wheel", StringComparison.OrdinalIgnoreCase))
                return VehicleDriveType.Fwd;
            if (text.Contains("rwd", StringComparison.OrdinalIgnoreCase)
                || text.Contains("rear-wheel", StringComparison.OrdinalIgnoreCase)
                || text.Contains("rear wheel", StringComparison.OrdinalIgnoreCase))
                return VehicleDriveType.Rwd;

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TitleCaseMake(string? make)
    {
        try
        {
            if (make is null) return null;
            if (make.Equals(make.ToUpperInvariant(), StringComparison.Ordinal))
                return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(make.ToLowerInvariant());
            return make;
        }
        catch
        {
            return make;
        }
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class NhtsaDecodeResponse
    {
        public List<NhtsaDecodeRow>? Results { get; set; }
    }

    private sealed class NhtsaDecodeRow
    {
        public string? Make { get; set; }
        public string? Model { get; set; }
        public string? ModelYear { get; set; }
        public string? DisplacementL { get; set; }
        public string? DriveType { get; set; }
        public string? TransmissionStyle { get; set; }
        public string? BodyClass { get; set; }
        public string? Trim { get; set; }
        public string? Series { get; set; }
        public string? EngineCylinders { get; set; }
        public string? Manufacturer { get; set; }
    }

    private sealed class NhtsaModelsResponse
    {
        public List<NhtsaModelRow>? Results { get; set; }
    }

    private sealed class NhtsaModelRow
    {
        public string? Model_Name { get; set; }
        public string? Make_Name { get; set; }
    }
}
