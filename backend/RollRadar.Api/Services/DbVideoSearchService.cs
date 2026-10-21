using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Athletes;
using RollRadar.Api.Common;
using RollRadar.Api.Curation;
using RollRadar.Api.Data;
using RollRadar.Api.Models;
using RollRadar.Api.Techniques;
using DbCurationStatus = RollRadar.Api.Data.CurationStatus;

namespace RollRadar.Api.Services;

/// <summary>
/// Same searches as <see cref="VideoSearchService"/>, over the moments imported into PostgreSQL by
/// RollRadar.Coletor. Moments are stored already merged (see MomentMerger), one row per moment.
/// An athlete of the catalog is searched by name and aliases as whole words in the title; anything else typed as
/// athlete, or any athlete while the catalog is empty, is a plain piece of the title.
/// </summary>
public class DbVideoSearchService(RollRadarDbContext db, TechniqueCatalog catalog, AthleteCatalog? athletes = null) : IVideoSearchService
{
    private readonly AthleteCatalog _athletes = athletes ?? AthleteCatalog.Empty;

    private record MomentRow(
        long Id, string VideoId, string TechniqueId, int StartSeconds, string Term, string Snippet, DbCurationStatus Status,
        string Title, DateOnly? UploadDate, string? Language, string Channel, int? DurationSeconds);

    public SearchResult Search(string? technique, string? athlete, bool includeRejected = false)
    {
        var query = technique?.Trim() ?? "";
        var resolved = catalog.Resolve(query);
        var athleteFilter = AthleteFilter(athlete);

        var moments = db.TechniqueMoments.AsNoTracking().Where(MomentFilter(query, resolved));
        if (!includeRejected) moments = moments.Where(m => m.CurationStatus != DbCurationStatus.Ruim);

        var videos = LoadVideos(moments)
            .Where(v => athleteFilter(v.Title))
            // DistinctBy: techniques that share a term (e.g. "Knee Shield" and "Z-Guard / Knee Shield") report the same moment.
            .Select(v => v with { Moments = v.Moments.DistinctBy(m => m.StartSeconds).ToList() })
            .OrderByDescending(v => v.Moments.Count)
            .ThenByDescending(v => v.UploadDate)
            .ThenBy(v => v.Title, StringComparer.Ordinal)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .ToList();

        var summaries = Summaries(resolved);
        var searchTerms = resolved.Count > 0
            ? resolved.SelectMany(t => t.EnglishTerms).Distinct().ToList()
            : query.Length > 0 ? [query] : [];

        return new SearchResult(query, summaries, searchTerms, videos);
    }

    public IReadOnlyList<TechniqueSummary> GetTechniques() => Summaries(catalog.All);

    public IReadOnlyList<AthleteSummary> GetAthletes()
    {
        var titles = db.Videos.AsNoTracking()
            .Where(v => v.Moments.Any())
            .Select(v => new { v.Id, v.Title })
            .AsEnumerable()
            .OrderBy(v => v.Id, StringComparer.Ordinal)
            .Select(v => v.Title)
            .ToList();
        if (_athletes.IsEmpty) return AthleteNames.FromTitles(titles);

        return _athletes.All
            .Select(a => new AthleteSummary(a.Name, titles.Count(t => _athletes.InTitle(a, t))) { Aliases = a.Aliases })
            .Where(a => a.Fights > 0)
            .OrderByDescending(a => a.Fights)
            .ThenBy(a => a.Name, StringComparer.Ordinal)
            .ToList();
    }

    private Func<string, bool> AthleteFilter(string? athlete)
    {
        if (_athletes.Resolve(athlete) is { } known) return title => _athletes.InTitle(known, title);
        var query = TextNormalizer.Normalize(athlete);
        return title => query.Length == 0 || TextNormalizer.Normalize(title).Contains(query);
    }

    public bool MomentExists(string momentId) =>
        MomentKeyParts.TryParse(momentId, out var key)
        && db.TechniqueMoments.Any(m => m.VideoId == key.VideoId && m.TechniqueId == key.TechniqueId && m.StartSeconds == key.StartSeconds);

    /// <summary>
    /// Moments are always stored with a catalog technique id, so a recognized technique filters by id. Otherwise,
    /// as in <see cref="VideoSearchService"/>, the query is looked up in the caption terms and the technique ids.
    /// </summary>
    private Expression<Func<TechniqueMomentEntity, bool>> MomentFilter(string query, IReadOnlyList<Technique> resolved)
    {
        if (query.Length == 0) return _ => true;

        if (resolved.Count > 0)
        {
            var ids = resolved.Select(t => t.Id).ToList();
            return m => ids.Contains(m.TechniqueId);
        }

        var normalizedQuery = TextNormalizer.Normalize(query);
        var idPart = Technique.ToId(query);
        // Few distinct terms: matched here with the same normalization as the file-based search.
        var terms = db.TechniqueMoments.Select(m => m.Term).Distinct().AsEnumerable()
            .Where(t => TextNormalizer.Normalize(t).Contains(normalizedQuery))
            .ToList();
        return m => terms.Contains(m.Term) || m.TechniqueId.Contains(idPart);
    }

    private List<FightVideo> LoadVideos(IQueryable<TechniqueMomentEntity> moments)
    {
        // Votes are few (people vote by hand), so they are loaded for all the moments at once and counted here.
        var votes = moments
            .SelectMany(m => m.Votes)
            .Select(v => new { v.MomentId, v.VoterId, v.IpHash, v.Useful, v.VotedAt })
            .AsEnumerable()
            .GroupBy(v => v.MomentId)
            .ToDictionary(g => g.Key, g => VoteTally.From(g.Select(v =>
                new MomentVote("", v.VoterId, v.IpHash, v.Useful, new DateTimeOffset(DateTime.SpecifyKind(v.VotedAt, DateTimeKind.Utc))))));

        return moments
            .Select(m => new MomentRow(
                m.Id, m.VideoId, m.TechniqueId, m.StartSeconds, m.Term, m.Snippet, m.CurationStatus,
                m.Video!.Title, m.Video.UploadDate, m.Video.Language, m.Video.Channel!.Name, m.Video.DurationSeconds))
            .AsEnumerable()
            .GroupBy(r => r.VideoId)
            .Select(g =>
            {
                var first = g.First();
                var moments = g
                    // Same order as the file-based merge, which read the technique files by name: by second,
                    // then by technique file name.
                    .OrderBy(r => r.StartSeconds)
                    .ThenBy(r => r.TechniqueId + ".json", StringComparer.Ordinal)
                    .Select(r =>
                    {
                        var tally = votes.GetValueOrDefault(r.Id, VoteTally.Empty);
                        return new TechniqueMoment(r.Term, r.StartSeconds, r.Snippet, r.TechniqueId)
                        {
                            Id = MomentKey.For(r.VideoId, r.TechniqueId, r.Term, r.StartSeconds),
                            Status = SnakeCase.From(r.Status.ToString()),
                            VotesUseful = tally.Useful,
                            VotesNotUseful = tally.NotUseful,
                        };
                    })
                    .ToList();
                return new FightVideo(
                    first.VideoId,
                    first.Title,
                    $"https://www.youtube.com/watch?v={first.VideoId}",
                    $"https://i.ytimg.com/vi/{first.VideoId}/mqdefault.jpg",
                    first.Channel,
                    first.UploadDate,
                    moments,
                    first.Language,
                    first.DurationSeconds);
            })
            .ToList();
    }

    /// <summary>
    /// Techniques with how many videos have a moment of each one and how many moments the curator approved.
    /// Moments marked as bad don't count, as they don't show up in the search.
    /// </summary>
    private List<TechniqueSummary> Summaries(IReadOnlyList<Technique> techniques)
    {
        var ids = techniques.Select(t => t.Id).ToList();
        var counts = db.TechniqueMoments
            .Where(m => ids.Contains(m.TechniqueId) && m.CurationStatus != DbCurationStatus.Ruim)
            .GroupBy(m => m.TechniqueId)
            .Select(g => new
            {
                g.Key,
                Videos = g.Select(m => m.VideoId).Distinct().Count(),
                Approved = g.Count(m => m.CurationStatus == DbCurationStatus.Bom),
            })
            .ToDictionary(x => x.Key);
        return techniques
            .Select(t => counts.GetValueOrDefault(t.Id) is { } c
                ? new TechniqueSummary(t.English, t.Portuguese, t.Category, c.Videos, t.Aliases, t.Id, c.Approved)
                : new TechniqueSummary(t.English, t.Portuguese, t.Category, 0, t.Aliases, t.Id))
            .ToList();
    }
}

/// <summary>The parts of a <see cref="MomentKey"/>: "a1b2c3.rear-naked-choke.66".</summary>
public record MomentKeyParts(string VideoId, string TechniqueId, int StartSeconds)
{
    public static bool TryParse(string? momentId, out MomentKeyParts key)
    {
        key = null!;
        var parts = momentId?.Split('.');
        if (parts is not [var video, var technique, var second] || video.Length == 0 || technique.Length == 0
            || !int.TryParse(second, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var start))
            return false;
        key = new MomentKeyParts(video, technique, start);
        return true;
    }
}
