using RollRadar.Api.Common;

namespace RollRadar.Api.Techniques;

/// <summary>
/// Translates what the user types (Portuguese, English or a known alias) into catalog techniques,
/// and tells whether a term highlighted in English captions refers to a technique.
/// </summary>
public class TechniqueCatalog
{
    /// <summary>
    /// Commentators often drop these words ("triangle" for "Triangle Choke", "de la riva" for "De La Riva Guard"),
    /// so a caption term also matches the name without them.
    /// </summary>
    private static readonly string[] OptionalSuffixes = ["choke", "guard", "sweep", "pass", "lock"];

    private readonly Dictionary<string, List<Technique>> _byName = new();
    private readonly Dictionary<Technique, HashSet<string>> _captionTerms = new();

    public TechniqueCatalog(IEnumerable<Technique> techniques)
    {
        All = techniques.ToList();
        foreach (var technique in All)
        {
            foreach (var name in technique.EnglishTerms.Append(technique.English).Append(technique.Portuguese).Concat(technique.Aliases))
            {
                var key = TextNormalizer.Normalize(name);
                if (key.Length == 0) continue;
                if (!_byName.TryGetValue(key, out var list)) _byName[key] = list = [];
                if (!list.Contains(technique)) list.Add(technique);
            }

            _captionTerms[technique] = technique.EnglishTerms.Concat(technique.Aliases)
                .Select(TextNormalizer.Normalize)
                .SelectMany(term => OptionalSuffixes
                    .Where(suffix => term.EndsWith(' ' + suffix))
                    .Select(suffix => term[..^(suffix.Length + 1)])
                    .Prepend(term))
                .Where(term => term.Length > 0)
                .ToHashSet();
        }
    }

    public IReadOnlyList<Technique> All { get; }

    /// <summary>Techniques whose English or Portuguese name, or alias, is exactly the query (ignoring case and accents).</summary>
    public IReadOnlyList<Technique> Resolve(string? query) =>
        _byName.TryGetValue(TextNormalizer.Normalize(query), out var techniques) ? techniques : [];

    /// <summary>Whether a term highlighted in the captions (e.g. "omoplata", "triangle") names the technique.</summary>
    public bool Matches(Technique technique, string captionTerm) =>
        _captionTerms[technique].Contains(TextNormalizer.Normalize(captionTerm));

    /// <summary>Technique by id, e.g. "rear-naked-choke".</summary>
    public Technique? FindById(string id) => All.FirstOrDefault(t => t.Id == id);

    /// <summary>Whether a caption moment belongs to the technique, by catalog id when the moment has one.</summary>
    public bool Matches(Technique technique, Models.TechniqueMoment moment) =>
        moment.TechniqueId is not null ? moment.TechniqueId == technique.Id : Matches(technique, moment.Technique);
}
