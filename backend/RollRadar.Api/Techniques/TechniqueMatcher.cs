using System.Text;
using System.Text.RegularExpressions;
using RollRadar.Api.Common;
using RollRadar.Api.Models;

namespace RollRadar.Api.Techniques;

/// <summary>One caption line: second where it starts and its text.</summary>
public record CaptionLine(double Start, string Text);

/// <summary>A mention of a technique in a video's captions.</summary>
/// <param name="Start">Second of the caption line where the technique name starts.</param>
/// <param name="Term">Term found, normalized, e.g. "armlock".</param>
/// <param name="Text">That line with the previous and the next one, for context.</param>
public record TechniqueHit(string TechniqueId, double Start, string Term, string Text);

/// <summary>
/// Finds the catalog techniques mentioned in captions. Port of montar_tecnicas.py, with the same rules so the
/// moments come out the same: generic terms skipped, only some aliases used as search terms, longest term first,
/// names split between two caption lines found, one hit per technique every <see cref="DedupWindowSeconds"/>.
/// </summary>
public partial class TechniqueMatcher
{
    /// <summary>Mentions of the same technique closer than this to the previous hit are skipped.</summary>
    public const double DedupWindowSeconds = 30;

    /// <summary>Terms so generic they show up in every fight and would only add noise.</summary>
    public static readonly IReadOnlySet<string> GenericTechniques = new HashSet<string>
    {
        "Grip", "Hook", "Frame", "Posture", "Base", "Pressure", "Leverage", "Transition",
        "Drill", "Tap / Tap Out", "Roll / Sparring", "Escape", "Bridge", "Sweep",
        "Takedown", "Submission", "Choke / Strangle", "Guard Pass", "Advantage",
        "Penalty", "Clinch", "Back Take",
    };

    /// <summary>Aliases from "tambem_conhecido_como" that work as search terms.</summary>
    public static readonly IReadOnlySet<string> SearchableAliases = new HashSet<string>
    {
        "Achilles lock", "Armlock", "Arrastão de braço", "Brabo", "Camarão",
        "Chave de calcanhar", "Cinquenta-cinquenta", "Controle lateral", "DLR", "RDLR",
        "De-ashi-barai", "Double leg", "Grande ceifada externa", "Grande ceifada interna",
        "Pequena ceifada interna", "Inside sankaku", "Kani-basami", "Kata-guruma",
        "Kimura no pé", "Loop choke", "Queda de ombro", "Queda de quadril", "Raspagem da borboleta",
        "Raspagem da flor", "RNC", "SLX", "Toureando", "Triângulo de braço", "Ukemi",
        "Z-guard", "juji-gatame", "knee slice",
    };

    /// <summary>Short forms people say that are not in the catalog, by English name.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ExtraTerms = new Dictionary<string, string[]>
    {
        ["De La Riva Guard"] = ["De La Riva"],
        ["Reverse De La Riva"] = ["Reverse De La Riva", "Reverse DLR"],
    };

    private sealed record SearchTechnique(string Id, IReadOnlyList<string> Terms, Regex Regex);

    private readonly IReadOnlyList<SearchTechnique> _techniques;

    public TechniqueMatcher(IEnumerable<Technique> catalog)
    {
        _techniques = catalog
            .Where(t => !GenericTechniques.Contains(t.English))
            .Select(ToSearch)
            .ToList();
    }

    /// <summary>The normalized search terms of each technique, longest first (techniques with no term are left out).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TermsById =>
        _techniques.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First().Terms);

    /// <summary>Every technique mention in the captions, technique by technique, each in caption order.</summary>
    public IReadOnlyList<TechniqueHit> Find(IReadOnlyList<CaptionLine> lines)
    {
        var normalized = lines.Select(l => Normalize(l.Text)).ToList();
        var hits = new List<TechniqueHit>();
        foreach (var technique in _techniques)
            hits.AddRange(FindTechnique(technique, lines, normalized));
        return hits;
    }

    /// <summary>
    /// The hits as API moments: second rounded down, same 20 s merging as <see cref="MomentMerger"/>, which is
    /// what the importer stored for the files written by montar_tecnicas.py.
    /// </summary>
    public IReadOnlyList<TechniqueMoment> FindMoments(IReadOnlyList<CaptionLine> lines) =>
        MomentMerger.Merge(Find(lines).Select(h =>
            new TechniqueMoment(h.Term, (int)Math.Floor(h.Start), h.Text.Trim(), h.TechniqueId)));

    private static IEnumerable<TechniqueHit> FindTechnique(
        SearchTechnique technique, IReadOnlyList<CaptionLine> lines, IReadOnlyList<string> normalized)
    {
        double? last = null;
        for (var i = 0; i < lines.Count; i++)
        {
            // The next line is appended only to find names split across two lines; the hit must start in this one.
            var current = normalized[i];
            var next = i + 1 < lines.Count ? normalized[i + 1] : "";
            var match = technique.Regex.Matches($"{current} {next}").FirstOrDefault(m => m.Index < current.Length);
            if (match is null) continue;
            if (last is not null && lines[i].Start - last < DedupWindowSeconds) continue;
            last = lines[i].Start;

            var context = lines.Skip(Math.Max(0, i - 1)).Take(i == 0 ? 2 : 3).Select(l => l.Text.Trim());
            var text = WhitespaceRegex().Replace(string.Join(" ", context), " ");
            yield return new TechniqueHit(technique.Id, lines[i].Start, match.Value, text);
        }
    }

    private static SearchTechnique ToSearch(Technique technique)
    {
        var names = new HashSet<string>(SlashRegex().Split(technique.English)) { technique.Portuguese };
        names.UnionWith(technique.Aliases.Where(SearchableAliases.Contains));
        if (ExtraTerms.TryGetValue(technique.English, out var extras)) names.UnionWith(extras);

        // Longest first, so the regex prefers "inside heel hook" to "heel hook"; ties in ordinal order, like Python.
        var terms = names
            .Select(Normalize)
            .Where(t => t.Length > 0 && !t.Replace(" ", "").All(char.IsDigit))
            .Distinct()
            .OrderByDescending(t => t.Length)
            .ThenBy(t => t, StringComparer.Ordinal)
            .ToList();
        var alternatives = string.Join("|", terms.Select(Regex.Escape));
        var regex = new Regex($"(?<![a-z0-9])(?:{alternatives})(?![a-z0-9])", RegexOptions.CultureInvariant);
        return new SearchTechnique(technique.Id, terms, regex);
    }

    /// <summary>
    /// norm() of montar_tecnicas.py: lower case, no accents, no apostrophes, hyphens as spaces, single spaces.
    /// Differs from <see cref="TextNormalizer.Normalize"/> in dropping apostrophes ("fireman's" -> "firemans").
    /// </summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in TextNormalizer.Normalize(text.Replace("'", "").Replace("’", "")))
        {
            var ch = char.IsWhiteSpace(c) ? ' ' : c;
            if (ch == ' ' && builder.Length > 0 && builder[^1] == ' ') continue;
            builder.Append(ch);
        }
        return builder.ToString().Trim();
    }

    [GeneratedRegex(@"\s+/\s+")]
    private static partial Regex SlashRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
