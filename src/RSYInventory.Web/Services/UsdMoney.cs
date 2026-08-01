using System.Globalization;

namespace RSYInventory.Web.Services;

/// <summary>USD display helpers (always $, never culture ¤).</summary>
public static class UsdMoney
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Format(decimal amount)
        => amount.ToString("C2", Us);

    public static string FormatOrDash(decimal? amount)
        => amount is null ? "—" : Format(amount.Value);
}
