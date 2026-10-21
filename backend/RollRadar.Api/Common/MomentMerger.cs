using RollRadar.Api.Models;

namespace RollRadar.Api.Common;

public static class MomentMerger
{
    /// <summary>Mentions of the same technique closer than this to the previous one are treated as the same moment.</summary>
    public const int WindowSeconds = 20;

    /// <summary>
    /// Keeps the first mention of each technique in every <see cref="WindowSeconds"/> window, ordered by time.
    /// Techniques are compared by catalog id when known, otherwise by the caption term.
    /// </summary>
    public static List<TechniqueMoment> Merge(IEnumerable<TechniqueMoment> moments)
    {
        var merged = new List<TechniqueMoment>();
        foreach (var group in moments.GroupBy(m => m.TechniqueId ?? TextNormalizer.Normalize(m.Technique)))
        {
            TechniqueMoment? last = null;
            foreach (var moment in group.OrderBy(m => m.StartSeconds))
            {
                if (last is not null && moment.StartSeconds - last.StartSeconds <= WindowSeconds) continue;
                merged.Add(moment);
                last = moment;
            }
        }
        return merged.OrderBy(m => m.StartSeconds).ToList();
    }

    /// <summary>Combines the same video coming from several sources into one, merging its moments.</summary>
    public static IReadOnlyList<FightVideo> MergeVideos(IEnumerable<FightVideo> videos) =>
        videos
            .GroupBy(v => v.Id)
            .Select(group =>
            {
                var first = group.FirstOrDefault(v => v.UploadDate is not null) ?? group.First();
                return first with
                {
                    Moments = Merge(group.SelectMany(v => v.Moments)),
                    Language = group.Select(v => v.Language).FirstOrDefault(l => l is not null)
                };
            })
            .ToList();
}
