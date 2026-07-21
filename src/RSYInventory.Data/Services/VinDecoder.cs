namespace RSYInventory.Data.Services;

/// <summary>
/// Best-effort VIN helpers shared by vehicles and parts (donor VIN).
/// </summary>
public static class VinDecoder
{
    public static (int? Year, string? Make) TryDecode(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin) || vin.Trim().Length < 10)
            return (null, null);

        vin = vin.Trim().ToUpperInvariant();
        var year = DecodeModelYear(vin[9]);
        var make = DecodeMake(vin[..3]);
        return (year, make);
    }

    public static string Normalize(string? vin)
        => string.IsNullOrWhiteSpace(vin) ? string.Empty : vin.Trim().ToUpperInvariant();

    private static int? DecodeModelYear(char code) => code switch
    {
        'A' => 2010, 'B' => 2011, 'C' => 2012, 'D' => 2013, 'E' => 2014,
        'F' => 2015, 'G' => 2016, 'H' => 2017, 'J' => 2018, 'K' => 2019,
        'L' => 2020, 'M' => 2021, 'N' => 2022, 'P' => 2023, 'R' => 2024,
        'S' => 2025, 'T' => 2026, 'V' => 2027, 'W' => 2028, 'X' => 2029,
        'Y' => 2030,
        '1' => 2001, '2' => 2002, '3' => 2003, '4' => 2004, '5' => 2005,
        '6' => 2006, '7' => 2007, '8' => 2008, '9' => 2009,
        _ => null
    };

    private static string? DecodeMake(string wmi) => wmi switch
    {
        "1G1" or "1G6" or "1GC" => "Chevrolet",
        "1FA" or "1FT" or "1FM" => "Ford",
        "2FA" or "2FM" => "Ford",
        "2ME" => "Mercury",
        "1J4" or "1C4" => "Jeep",
        "2T1" or "4T1" or "5TD" => "Toyota",
        "3VW" or "1VW" => "Volkswagen",
        "5YJ" => "Tesla",
        "JM1" or "3MZ" => "Mazda",
        "KNA" or "5XY" => "Kia",
        "5NP" or "KM8" => "Hyundai",
        "1N4" or "3N1" => "Nissan",
        "WBA" or "WBS" => "BMW",
        "WDB" or "WDD" => "Mercedes-Benz",
        "JH4" or "19U" => "Acura",
        "JHM" or "1HG" => "Honda",
        _ => null
    };
}
