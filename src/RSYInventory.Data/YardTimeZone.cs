namespace RSYInventory.Data;

/// <summary>
/// Yard operates on North Carolina local time (US Eastern).
/// </summary>
public static class YardTimeZone
{
    private static readonly Lazy<TimeZoneInfo> Eastern = new(ResolveEastern);

    public static TimeZoneInfo EasternInfo => Eastern.Value;

    public static DateTime NowEastern() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternInfo);

    public static DateOnly TodayEastern() => DateOnly.FromDateTime(NowEastern());

    public static DateTime ToEastern(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            EasternInfo);

    private static TimeZoneInfo ResolveEastern()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
    }
}
