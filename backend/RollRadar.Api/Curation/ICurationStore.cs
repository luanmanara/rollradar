namespace RollRadar.Api.Curation;

/// <summary>Visitors' votes and the curator's verdict on each moment.</summary>
public interface ICurationStore
{
    /// <summary>Adds the visitor's vote on the moment, or replaces the one they already gave.</summary>
    void SaveVote(MomentVote vote);

    void RemoveVote(string momentId, string voterId);

    MomentVote? GetVote(string momentId, string voterId);

    VoteTally GetTally(string momentId);

    /// <summary><see cref="CurationStatus.Pending"/> until the curator decides.</summary>
    string GetStatus(string momentId);

    void SetStatus(string momentId, string status);
}
