namespace RollRadar.Api.Training;

/// <summary>
/// The training diary of each user. Every method takes the signed-in user's id (from the session cookie, never from
/// the address) and only sees that user's rows: someone else's session answers as not found.
/// </summary>
public interface ITrainingStore
{
    Task<TrainingPage> ListAsync(Guid userId, TrainingFilter filter, CancellationToken cancellationToken = default);

    Task<TrainingSession?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>Stores a new session; when the id is already the user's, returns that one untouched.</summary>
    Task<SaveResult> CreateAsync(Guid userId, TrainingSessionInput input, CancellationToken cancellationToken = default);

    Task<SaveResult> UpdateAsync(Guid userId, Guid id, TrainingSessionInput input, CancellationToken cancellationToken = default);

    /// <summary>False when the user has no such session.</summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>The most recent point to fix not answered yet, or null.</summary>
    Task<FocusPoint?> GetOpenFocusAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary><see cref="SaveStatus.Updated"/>, <see cref="SaveStatus.NotFound"/> or <see cref="SaveStatus.Invalid"/>.</summary>
    Task<SaveResult> ReviewFocusAsync(Guid userId, long id, FocusReviewInput input, CancellationToken cancellationToken = default);

    /// <summary>The user's partners whose name contains the text (case and accents ignored), most recent first.</summary>
    Task<IReadOnlyList<Partner>> FindPartnersAsync(Guid userId, string? query, CancellationToken cancellationToken = default);

    Task<RecentPositions> GetRecentPositionsAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Null when the user does not exist.</summary>
    Task<AccountExport?> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
}
