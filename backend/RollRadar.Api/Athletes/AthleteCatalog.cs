using RollRadar.Api.Common;

namespace RollRadar.Api.Athletes;

/// <summary>An athlete of the catalog and the other ways their name is written in fight titles.</summary>
/// <param name="Name">Name as shown on the site, e.g. "Mica Galvão".</param>
/// <param name="Aliases">e.g. "Mica Galvao", "Micael Galvão".</param>
public record Athlete(string Name, IReadOnlyList<string> Aliases)
{
    public IEnumerable<string> Names => Aliases.Prepend(Name);
}

/// <summary>
/// The athletes reviewed by hand (athletes table, edited through the collector's CSV). A fight belongs to an
/// athlete when their name or an alias appears in its title as whole words, ignoring case and accents.
/// </summary>
public class AthleteCatalog
{
    private readonly Dictionary<string, Athlete> _byName = new();
    private readonly Dictionary<Athlete, List<string>> _titleTerms = new();

    public AthleteCatalog(IEnumerable<Athlete> athletes)
    {
        All = athletes.ToList();
        foreach (var athlete in All)
        {
            foreach (var name in athlete.Names.Select(TextNormalizer.Normalize).Where(n => n.Length > 0))
                _byName.TryAdd(name, athlete);
            _titleTerms[athlete] = athlete.Names.Select(Words).Where(w => w.Length > 0).Distinct().Select(w => $" {w} ").ToList();
        }
    }

    public static AthleteCatalog Empty { get; } = new([]);

    public IReadOnlyList<Athlete> All { get; }

    public bool IsEmpty => All.Count == 0;

    /// <summary>The athlete whose name or alias is exactly the query (ignoring case and accents).</summary>
    public Athlete? Resolve(string? query) => _byName.GetValueOrDefault(TextNormalizer.Normalize(query));

    /// <summary>Whether the fight title names the athlete.</summary>
    public bool InTitle(Athlete athlete, string title)
    {
        var padded = $" {Words(title)} ";
        return _titleTerms[athlete].Any(padded.Contains);
    }

    /// <summary>Whether the name is already in the catalog, as a name or an alias.</summary>
    public bool Covers(string name) => _byName.ContainsKey(TextNormalizer.Normalize(name));

    /// <summary>"Mica Galvão vs. Tye (Atos) | WNO" -> "mica galvao vs tye atos wno", so names match as whole words.</summary>
    private static string Words(string title) =>
        string.Join(' ', TextNormalizer.Normalize(title).Split(Separators, StringSplitOptions.RemoveEmptyEntries));

    private static readonly char[] Separators = [' ', '.', ',', ':', ';', '|', '/', '(', ')', '[', ']', '!', '?', '"', '“', '”'];
}
