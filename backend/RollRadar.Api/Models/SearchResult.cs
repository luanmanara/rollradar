namespace RollRadar.Api.Models;

/// <summary>A technique and how many loaded videos mention it.</summary>
/// <param name="Aliases">Other names from the catalog (e.g. "RNC", "armlock"), useful for autocomplete.</param>
/// <param name="Id">Technique id ("rear-naked-choke"), used in the site's technique page address.</param>
/// <param name="ApprovedMoments">Moments the curator marked as good.</param>
public record TechniqueSummary(
    string English, string Portuguese, string Category, int VideoCount, IReadOnlyList<string> Aliases,
    string Id = "", int ApprovedMoments = 0);

/// <summary>An athlete of the catalog (or, while it is empty, found in the fight titles), to search by athlete.</summary>
/// <param name="Fights">Fights with moments that have the athlete in the title.</param>
public record AthleteSummary(string Name, int Fights)
{
    /// <summary>Other spellings from the catalog, so the search box also finds "Buchecha" or "Galvao".</summary>
    public IReadOnlyList<string> Aliases { get; init; } = [];
}

/// <param name="Query">Technique as typed by the user.</param>
/// <param name="Techniques">Catalog techniques the query was recognized as (empty when not in the catalog).</param>
/// <param name="SearchTerms">English terms looked up in the captions.</param>
/// <param name="Videos">Matching videos, keeping only the moments of the searched technique.</param>
public record SearchResult(
    string Query,
    IReadOnlyList<TechniqueSummary> Techniques,
    IReadOnlyList<string> SearchTerms,
    IReadOnlyList<FightVideo> Videos);
