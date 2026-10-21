using RollRadar.Api.Models;

namespace RollRadar.Api.Curation;

public record VoteRequest(bool Useful);

/// <summary>The visitor's own vote on a moment; null when they haven't voted.</summary>
public record MyVote(bool? Useful);

public record StatusRequest(string Status);

/// <summary>A moment waiting for (or given) the curator's verdict.</summary>
/// <param name="Video">The video, with only this moment.</param>
public record CurationItem(FightVideo Video, TechniqueMoment Moment, VoteTally Votes, string Status);
