namespace RollRadar.Api.Models;

/// <summary>One page of a list; pass <paramref name="Next"/> as <c>after</c> to get the next one (null on the last page).</summary>
public record DataPage<T>(IReadOnlyList<T> Items, string? Next);

public record DataTechnique(
    string Id, string English, string Portuguese, string Category, IReadOnlyList<string> EnglishTerms, IReadOnlyList<string> Aliases);

/// <param name="HasTranscript">Whether the English captions are stored (see the transcript endpoint).</param>
public record DataVideo(
    string Id, string Title, string Url, string Channel, DateOnly? UploadDate, int? DurationSeconds, string? Language,
    bool HasTranscript, int MomentCount);

/// <param name="Id">Same moment id as the site uses (<see cref="Curation.MomentKey"/>).</param>
/// <param name="CurationStatus">pendente, bom or ruim: the curator's verdict.</param>
/// <param name="VotesUseful">Visitors' votes, each IP address counted once (its latest vote).</param>
public record DataMoment(
    string Id, string VideoId, string TechniqueId, int StartSeconds, string Term, string Snippet,
    string CurationStatus, DateTime? CuratedAt, int VotesUseful, int VotesNotUseful);

public record DataTranscriptSegment(double StartSeconds, double? DurationSeconds, string Text);

public record DataTranscript(string VideoId, IReadOnlyList<DataTranscriptSegment> Segments);
