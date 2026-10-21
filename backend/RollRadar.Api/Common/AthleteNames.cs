using System.Text.RegularExpressions;
using RollRadar.Api.Models;

namespace RollRadar.Api.Common;

/// <summary>
/// Athlete names guessed from fight titles such as "Gordon Ryan vs Felipe Pena | ADCC 2024" or
/// "A vs B / Manaus Open 2026": the part before the event, split at "vs", without a lead-in such as "Full Match:".
/// Titles in other formats give no names. Used as is when the athlete catalog is empty, and to suggest names for it.
/// </summary>
public static partial class AthleteNames
{
    /// <summary>The two athletes of a title, or none when it is not "A vs B".</summary>
    public static IReadOnlyList<string> FromTitle(string title)
    {
        var sides = VsRegex().Split(EventSeparatorRegex().Split(title)[0]);
        if (sides.Length != 2) return [];
        return sides
            .Select(side => SuffixRegex().Replace(LeadInRegex().Replace(side, ""), "").Trim())
            .Where(name => name.Length is >= 3 and <= 40)
            .ToList();
    }

    /// <summary>
    /// Each athlete once, with how many fights have them, under the most common spelling (on a tie, the one with
    /// accents, then the one seen first).
    /// </summary>
    public static IReadOnlyList<AthleteSummary> FromTitles(IEnumerable<string> titles) =>
        titles
            .SelectMany(FromTitle)
            .GroupBy(TextNormalizer.Normalize)
            .Select(g => new AthleteSummary(
                g.GroupBy(name => name)
                    .OrderByDescending(s => s.Count())
                    .ThenByDescending(s => s.Key.Count(c => c > 127))
                    .First().Key,
                g.Count()))
            .OrderByDescending(a => a.Fights)
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"(?<=\S)\s+(?:vs\.?|v\.s\.|versus)\s+(?=\S)", RegexOptions.IgnoreCase)]
    private static partial Regex VsRegex();

    /// <summary>"Full Match: ", "Recapping WNO 28: " before the first name.</summary>
    [GeneratedRegex(@"^.*:\s+")]
    private static partial Regex LeadInRegex();

    [GeneratedRegex(@"\s+[/|]\s+")]
    private static partial Regex EventSeparatorRegex();

    /// <summary>"- Final" or "(Atos)" after the name; hyphens inside names (Jean-Jacques) stay.</summary>
    [GeneratedRegex(@"\s+[-–]\s.*$|\s*\(.*$")]
    private static partial Regex SuffixRegex();
}
