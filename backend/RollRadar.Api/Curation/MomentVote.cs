namespace RollRadar.Api.Curation;

/// <summary>An anonymous visitor's opinion on whether a moment really shows the technique.</summary>
/// <param name="VoterId">Random id kept in the visitor's signed cookie.</param>
/// <param name="IpHash">Keyed hash of the visitor's IP address; the address itself is never stored.</param>
public record MomentVote(string MomentId, string VoterId, string IpHash, bool Useful, DateTimeOffset VotedAt);

/// <summary>Votes on a moment, where each IP address counts once (its latest vote).</summary>
public record VoteTally(int Useful, int NotUseful)
{
    public static readonly VoteTally Empty = new(0, 0);

    public int Total => Useful + NotUseful;

    /// <summary>Counts the latest vote of each IP address, so clearing the cookie does not add votes.</summary>
    public static VoteTally From(IEnumerable<MomentVote> votes)
    {
        var perIp = votes
            .GroupBy(v => v.IpHash)
            .Select(g => g.MaxBy(v => v.VotedAt)!.Useful)
            .ToList();
        return new(perIp.Count(useful => useful), perIp.Count(useful => !useful));
    }
}
