using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Curation;
using RollRadar.Api.Services;
using DbCurationStatus = RollRadar.Api.Data.CurationStatus;

namespace RollRadar.Api.Data;

/// <summary>
/// Votes in moment_votes and the curator's verdict in technique_moments.curation_status. The API's moment id
/// ("{videoId}.{techniqueId}.{startSeconds}") is looked up through the unique index on those three columns.
/// </summary>
public class EfCurationStore(RollRadarDbContext db) : ICurationStore
{
    private static readonly Dictionary<string, DbCurationStatus> Statuses =
        Enum.GetValues<DbCurationStatus>().ToDictionary(s => SnakeCase.From(s.ToString()));

    public void SaveVote(MomentVote vote)
    {
        if (FindMomentId(vote.MomentId) is not { } momentId) return;

        // One row per (moment, voter): a second vote replaces the first.
        db.Database.ExecuteSqlInterpolated($"""
            INSERT INTO moment_votes (moment_id, voter_id, ip_hash, useful, voted_at)
            VALUES ({momentId}, {vote.VoterId}, {vote.IpHash}, {vote.Useful}, {vote.VotedAt.UtcDateTime})
            ON CONFLICT (moment_id, voter_id)
            DO UPDATE SET ip_hash = excluded.ip_hash, useful = excluded.useful, voted_at = excluded.voted_at
            """);
    }

    public void RemoveVote(string momentId, string voterId)
    {
        if (FindMomentId(momentId) is not { } id) return;
        db.MomentVotes.Where(v => v.MomentId == id && v.VoterId == voterId).ExecuteDelete();
    }

    public MomentVote? GetVote(string momentId, string voterId)
    {
        if (FindMomentId(momentId) is not { } id) return null;
        return Votes(momentId, id).FirstOrDefault(v => v.VoterId == voterId);
    }

    public VoteTally GetTally(string momentId) =>
        FindMomentId(momentId) is { } id ? VoteTally.From(Votes(momentId, id)) : VoteTally.Empty;

    public string GetStatus(string momentId)
    {
        if (!MomentKeyParts.TryParse(momentId, out var key)) return Curation.CurationStatus.Pending;
        var status = Moment(key).Select(m => (DbCurationStatus?)m.CurationStatus).FirstOrDefault();
        return status is { } s ? SnakeCase.From(s.ToString()) : Curation.CurationStatus.Pending;
    }

    public void SetStatus(string momentId, string status)
    {
        if (!MomentKeyParts.TryParse(momentId, out var key) || !Statuses.TryGetValue(status, out var value)) return;
        DateTime? curatedAt = value == DbCurationStatus.Pendente ? null : DateTime.UtcNow;
        Moment(key).ExecuteUpdate(s => s
            .SetProperty(m => m.CurationStatus, value)
            .SetProperty(m => m.CuratedAt, curatedAt));
    }

    private IQueryable<TechniqueMomentEntity> Moment(MomentKeyParts key) =>
        db.TechniqueMoments.Where(m =>
            m.VideoId == key.VideoId && m.TechniqueId == key.TechniqueId && m.StartSeconds == key.StartSeconds);

    private long? FindMomentId(string momentId) =>
        MomentKeyParts.TryParse(momentId, out var key) ? Moment(key).Select(m => (long?)m.Id).FirstOrDefault() : null;

    private List<MomentVote> Votes(string momentId, long id) =>
        db.MomentVotes
            .AsNoTracking()
            .Where(v => v.MomentId == id)
            .AsEnumerable()
            .Select(v => new MomentVote(momentId, v.VoterId, v.IpHash, v.Useful,
                new DateTimeOffset(DateTime.SpecifyKind(v.VotedAt, DateTimeKind.Utc))))
            .ToList();
}
