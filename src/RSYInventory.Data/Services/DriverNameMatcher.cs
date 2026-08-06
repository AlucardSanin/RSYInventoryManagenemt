using System.Globalization;
using System.Text;

namespace RSYInventory.Data.Services;

/// <summary>
/// Maps free-text pickup driver names (legacy) to registered driver users.
/// </summary>
public static class DriverNameMatcher
{
    /// <summary>Known typos / short names → preferred display-name fragment to look up.</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ulyces"] = "ulysses",
        ["ulyses"] = "ulysses",
        ["ulysse"] = "ulysses",
        ["ulysses"] = "ulysses",
        ["brayant"] = "brayant",
        ["bryant"] = "brayant",
        ["bryan"] = "brayant",
        ["david"] = "david johnson",
        ["miguel"] = "miguel",
    };

    public static int? Match(
        string? freeText,
        IReadOnlyList<(int Id, string DisplayName)> drivers)
    {
        if (string.IsNullOrWhiteSpace(freeText) || drivers.Count == 0)
            return null;

        var needle = Normalize(freeText);
        if (needle.Length == 0)
            return null;

        // 1) Exact display-name match
        foreach (var d in drivers)
        {
            if (Normalize(d.DisplayName) == needle)
                return d.Id;
        }

        // 2) Alias → resolve against display names
        if (Aliases.TryGetValue(needle, out var aliasTarget)
            || Aliases.TryGetValue(FirstToken(needle), out aliasTarget))
        {
            var aliasNorm = Normalize(aliasTarget);
            var aliasHits = drivers
                .Where(d =>
                {
                    var n = Normalize(d.DisplayName);
                    return n == aliasNorm || n.StartsWith(aliasNorm + " ", StringComparison.Ordinal)
                           || n.Contains(aliasNorm, StringComparison.Ordinal);
                })
                .ToList();
            if (aliasHits.Count == 1)
                return aliasHits[0].Id;
            if (aliasHits.Count > 1)
            {
                var exact = aliasHits.FirstOrDefault(d => Normalize(d.DisplayName) == aliasNorm);
                if (exact.Id != 0)
                    return exact.Id;
            }
        }

        // 3) Free text contained in display name or vice versa
        var contains = drivers
            .Where(d =>
            {
                var n = Normalize(d.DisplayName);
                return n.Contains(needle, StringComparison.Ordinal)
                       || needle.Contains(n, StringComparison.Ordinal);
            })
            .ToList();
        if (contains.Count == 1)
            return contains[0].Id;

        // 4) First-token match when unique among drivers
        var token = FirstToken(needle);
        if (token.Length >= 3)
        {
            var firstHits = drivers
                .Where(d => FirstToken(Normalize(d.DisplayName)) == token)
                .ToList();
            if (firstHits.Count == 1)
                return firstHits[0].Id;
        }

        return null;
    }

    public static string Normalize(string value)
    {
        var formD = value.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsWhiteSpace(ch))
            {
                if (sb.Length > 0 && sb[^1] != ' ')
                    sb.Append(' ');
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Trim();
    }

    private static string FirstToken(string normalized)
    {
        var i = normalized.IndexOf(' ');
        return i < 0 ? normalized : normalized[..i];
    }
}
