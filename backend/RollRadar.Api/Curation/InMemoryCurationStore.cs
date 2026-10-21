namespace RollRadar.Api.Curation;

/// <summary>
/// Keeps votes and verdicts in memory, so they are lost when the API restarts. Stand-in until the
/// PostgreSQL database (banco-de-dados/plano.md) is in place.
/// </summary>
public class InMemoryCurationStore : ICurationStore
{
    private readonly object _lock = new();
    /// <summary>Moment id -> voter id -> vote.</summary>
    private readonly Dictionary<string, Dictionary<string, MomentVote>> _votes = [];
    private readonly Dictionary<string, string> _statuses = [];

    public void SaveVote(MomentVote vote)
    {
        lock (_lock)
        {
            if (!_votes.TryGetValue(vote.MomentId, out var byVoter)) _votes[vote.MomentId] = byVoter = [];
            byVoter[vote.VoterId] = vote;
        }
    }

    public void RemoveVote(string momentId, string voterId)
    {
        lock (_lock) _votes.GetValueOrDefault(momentId)?.Remove(voterId);
    }

    public MomentVote? GetVote(string momentId, string voterId)
    {
        lock (_lock) return _votes.GetValueOrDefault(momentId)?.GetValueOrDefault(voterId);
    }

    public VoteTally GetTally(string momentId)
    {
        lock (_lock)
            return _votes.TryGetValue(momentId, out var byVoter) ? VoteTally.From(byVoter.Values) : VoteTally.Empty;
    }

    public string GetStatus(string momentId)
    {
        lock (_lock) return _statuses.GetValueOrDefault(momentId, CurationStatus.Pending);
    }

    public void SetStatus(string momentId, string status)
    {
        lock (_lock)
        {
            if (status == CurationStatus.Pending) _statuses.Remove(momentId);
            else _statuses[momentId] = status;
        }
    }
}
