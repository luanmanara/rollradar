using System.Text.RegularExpressions;
using RollRadar.Api.Common;

namespace RollRadar.Api.Techniques;

/// <summary>A technique from the catalog, with the English terms used to find it in captions.</summary>
/// <param name="English">Name as written in the catalog, e.g. "Back Control / Back Mount".</param>
/// <param name="Portuguese">Portuguese name.</param>
/// <param name="Category">e.g. "Chaves de braço".</param>
/// <param name="EnglishTerms">The English name split on " / ", e.g. ["Back Control", "Back Mount"].</param>
/// <param name="Aliases">Other names from "tambem_conhecido_como", in Portuguese or English.</param>
public partial record Technique(
    string English,
    string Portuguese,
    string Category,
    IReadOnlyList<string> EnglishTerms,
    IReadOnlyList<string> Aliases)
{
    /// <summary>
    /// "Rear Naked Choke" -> "rear-naked-choke". Same id as montar_tecnicas.py, which names
    /// its output files after it.
    /// </summary>
    public string Id { get; } = ToId(English);

    public static string ToId(string english) =>
        NonAlphanumericRegex().Replace(TextNormalizer.Normalize(english), "-").Trim('-');

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();
}
