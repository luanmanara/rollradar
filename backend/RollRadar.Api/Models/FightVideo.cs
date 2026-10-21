namespace RollRadar.Api.Models;

/// <param name="DurationSeconds">Length of the video, when the collector recorded it; places the moments on a timeline.</param>
public record FightVideo(
    string Id,
    string Title,
    string Url,
    string ThumbnailUrl,
    string Channel,
    DateOnly? UploadDate,
    IReadOnlyList<TechniqueMoment> Moments,
    string? Language = null,
    int? DurationSeconds = null);

/// <summary>A moment in a video where a technique is mentioned in the captions.</summary>
/// <param name="Technique">Term found in the captions, e.g. "Omoplata" or "armlock".</param>
/// <param name="StartSeconds">Second where the caption line with the term starts.</param>
/// <param name="Snippet">Caption text around the mention.</param>
/// <param name="TechniqueId">Catalog technique id ("rear-naked-choke") when known.</param>
public record TechniqueMoment(string Technique, int StartSeconds, string Snippet, string? TechniqueId = null)
{
    /// <summary>Stable id used to vote on and curate this moment; see <see cref="Curation.MomentKey"/>.</summary>
    public string Id { get; init; } = "";

    /// <summary>The curator's verdict: pendente, bom or ruim (<see cref="Curation.CurationStatus"/>).</summary>
    public string Status { get; init; } = Curation.CurationStatus.Pending;

    /// <summary>Visitors' votes, each IP address counted once (its latest vote).</summary>
    public int VotesUseful { get; init; }

    public int VotesNotUseful { get; init; }
}
