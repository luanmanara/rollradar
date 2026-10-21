using RollRadar.Api.Common;
using RollRadar.Api.Curation;
using RollRadar.Api.Models;
using RollRadar.Api.Techniques;
using RollRadar.Api.Transcripts;
using Microsoft.Extensions.Options;

namespace RollRadar.Api.Services;

/// <summary>
/// Searches the videos of the per-technique files written by montar_tecnicas.py, loaded once at startup.
/// Captions are in English, so a Portuguese query is translated with the technique catalog.
/// </summary>
public class VideoSearchService : IVideoSearchService
{
    private readonly IReadOnlyList<FightVideo> _videos;
    private readonly TechniqueCatalog _catalog;
    private readonly Lazy<IReadOnlyList<TechniqueSummary>> _techniques;
    private readonly HashSet<string> _momentIds;

    public VideoSearchService(
        IOptions<TranscriptOptions> transcriptOptions,
        TechniqueCatalog catalog,
        IHostEnvironment env,
        ILogger<VideoSearchService> logger)
        : this(Load(Path.GetFullPath(Path.Combine(env.ContentRootPath, transcriptOptions.Value.OutputPath)), catalog, logger), catalog)
    {
    }

    public VideoSearchService(IReadOnlyList<FightVideo> videos, TechniqueCatalog catalog)
    {
        _videos = videos
            .Select(v => v with
            {
                Moments = v.Moments
                    .Select(m => m with { Id = MomentKey.For(v.Id, m.TechniqueId, m.Technique, m.StartSeconds) })
                    .ToList()
            })
            .ToList();
        _momentIds = _videos.SelectMany(v => v.Moments).Select(m => m.Id).ToHashSet();
        _catalog = catalog;
        _techniques = new(BuildTechniqueSummaries);
    }

    private static IReadOnlyList<FightVideo> Load(string folder, TechniqueCatalog catalog, ILogger logger)
    {
        if (!Directory.Exists(folder))
        {
            logger.LogWarning("Technique files folder not found: {Folder}. Run samples/montar_tecnicas.py.", folder);
            return [];
        }

        var videos = TechniqueFileLoader.Load(Directory.GetFiles(folder, "*.json"), catalog, logger);
        logger.LogInformation("Loaded {Videos} videos from technique files in {Folder}", videos.Count, folder);
        return videos;
    }

    /// <remarks>The technique files have no curation, so <paramref name="includeRejected"/> changes nothing.</remarks>
    public SearchResult Search(string? technique, string? athlete, bool includeRejected = false)
    {
        var query = technique?.Trim() ?? "";
        var resolved = _catalog.Resolve(query);
        var athleteQuery = TextNormalizer.Normalize(athlete);

        Func<TechniqueMoment, bool> isSearched = query.Length == 0
            ? _ => true
            : resolved.Count > 0
                ? m => resolved.Any(t => _catalog.Matches(t, m))
                : m => TextNormalizer.Normalize(m.Technique).Contains(TextNormalizer.Normalize(query))
                       || (m.TechniqueId?.Contains(Technique.ToId(query)) ?? false);

        var videos = _videos
            .Where(v => athleteQuery.Length == 0 || TextNormalizer.Normalize(v.Title).Contains(athleteQuery))
            // DistinctBy: techniques that share a term (e.g. "Knee Shield" and "Z-Guard / Knee Shield") report the same moment.
            .Select(v => v with { Moments = v.Moments.Where(isSearched).DistinctBy(m => m.StartSeconds).ToList() })
            .Where(v => v.Moments.Count > 0)
            .OrderByDescending(v => v.Moments.Count)
            .ThenByDescending(v => v.UploadDate)
            .ToList();

        var summaries = resolved.Select(t => _techniques.Value.First(s => s.English == t.English)).ToList();
        var searchTerms = resolved.Count > 0
            ? resolved.SelectMany(t => t.EnglishTerms).Distinct().ToList()
            : query.Length > 0 ? [query] : [];

        return new SearchResult(query, summaries, searchTerms, videos);
    }

    public IReadOnlyList<TechniqueSummary> GetTechniques() => _techniques.Value;

    public IReadOnlyList<AthleteSummary> GetAthletes() =>
        AthleteNames.FromTitles(_videos.Where(v => v.Moments.Count > 0).OrderBy(v => v.Id, StringComparer.Ordinal).Select(v => v.Title));

    public bool MomentExists(string momentId) => _momentIds.Contains(momentId);

    private IReadOnlyList<TechniqueSummary> BuildTechniqueSummaries()
    {
        var fromCatalog = _catalog.All.Select(t => new TechniqueSummary(
            t.English,
            t.Portuguese,
            t.Category,
            _videos.Count(v => v.Moments.Any(m => _catalog.Matches(t, m))),
            t.Aliases,
            t.Id));

        var notInCatalog = _videos
            .SelectMany(v => v.Moments.Select(m => (Video: v.Id, Moment: m)))
            .Where(x => !_catalog.All.Any(t => _catalog.Matches(t, x.Moment)))
            .GroupBy(x => x.Moment.TechniqueId ?? x.Moment.Technique)
            .Select(g => new TechniqueSummary(g.Key, "", "", g.Select(x => x.Video).Distinct().Count(), [], g.Key));

        return fromCatalog.Concat(notInCatalog).ToList();
    }
}
