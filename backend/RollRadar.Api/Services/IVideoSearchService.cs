using RollRadar.Api.Models;

namespace RollRadar.Api.Services;

public interface IVideoSearchService
{
    /// <summary>
    /// Searches by technique, typed in Portuguese or English, and/or athlete. Moments the curator marked as bad
    /// are left out unless <paramref name="includeRejected"/> (the curation page lists them too).
    /// </summary>
    SearchResult Search(string? technique, string? athlete, bool includeRejected = false);

    /// <summary>
    /// Several techniques at once: the fights with a moment of any of them, those with more of the techniques
    /// first, each fight keeping the moments of all of them in time order.
    /// </summary>
    SearchResult SearchMany(IReadOnlyList<string> techniques, string? athlete)
    {
        var results = techniques.Select(t => Search(t, athlete)).ToList();
        var videos = results
            .SelectMany(r => r.Videos)
            .GroupBy(v => v.Id)
            .Select(g => g.First() with
            {
                Moments = g.SelectMany(v => v.Moments).DistinctBy(m => m.Id)
                    .OrderBy(m => m.StartSeconds).ThenBy(m => m.TechniqueId, StringComparer.Ordinal).ToList(),
            })
            .OrderByDescending(v => v.Moments.Select(m => m.TechniqueId ?? m.Technique).Distinct().Count())
            .ThenByDescending(v => v.Moments.Count)
            .ThenByDescending(v => v.UploadDate)
            .ThenBy(v => v.Title, StringComparer.Ordinal)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .ToList();
        return new SearchResult(
            string.Join(" + ", results.Select(r => r.Query)),
            results.SelectMany(r => r.Techniques).DistinctBy(t => t.English).ToList(),
            results.SelectMany(r => r.SearchTerms).Distinct().ToList(),
            videos);
    }

    /// <summary>Athletes of the fights with moments, from their titles (<see cref="Common.AthleteNames"/>), most fights first.</summary>
    IReadOnlyList<AthleteSummary> GetAthletes();

    /// <summary>Catalog techniques plus any technique found in the data that is not in the catalog.</summary>
    IReadOnlyList<TechniqueSummary> GetTechniques();

    /// <summary>Whether a loaded video has a moment with this <see cref="TechniqueMoment.Id"/>.</summary>
    bool MomentExists(string momentId);
}
