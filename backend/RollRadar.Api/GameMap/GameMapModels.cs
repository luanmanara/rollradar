using RollRadar.Api.Models;
using RollRadar.Api.Training;

namespace RollRadar.Api.GameMap;

// What the game map API receives and returns (/api/mapa). Kinds and sides travel as the stored snake_case names
// ("raspagem", "finalizacao", "por_baixo"...), see Data/TrainingEntities.cs.

/// <param name="SuccessRate">Fui bem / (fui bem + sofri); null while neither was marked.</param>
public record NodeStats(int Steps, int Good, int Bad, int FinishesFor, int FinishesAgainst, double? SuccessRate);

/// <param name="Uses">Times the transition was tried in detailed rolls, or times the submission happened.</param>
/// <param name="SuccessRate">Transitions that worked / tries; null for submissions and unused arrows.</param>
public record EdgeStats(int Uses, int Succeeded, double? SuccessRate);

public record MapNode(long Id, string TechniqueId, string? Label, string Side, bool Hidden, double? X, double? Y, NodeStats Stats);

public record MapEdge(
    long Id, long FromNodeId, long? ToNodeId, string Kind, string? TechniqueId, string? Label, bool AgainstMe, EdgeStats Stats);

/// <summary>A strong or weak point of the game: a position or a transition.</summary>
/// <param name="Target">"node" or "edge".</param>
/// <param name="Score">Share of good outcomes (strong) or of bad ones (weak), 0 to 1.</param>
/// <param name="Occurrences">How many times it came up; highlights need at least <see cref="GameMapStats.MinOccurrences"/>.</param>
public record Highlight(string Target, long Id, double Score, int Occurrences);

public record Highlights(IReadOnlyList<Highlight> Strong, IReadOnlyList<Highlight> Weak);

/// <summary>The whole map of the user: positions (hidden ones included, flagged), arrows and highlights.</summary>
public record GameMapView(IReadOnlyList<MapNode> Nodes, IReadOnlyList<MapEdge> Edges, Highlights Highlights);

/// <summary>Notes of a roll that went through a position or an arrow, to open the session they came from.</summary>
public record MapNote(Guid SessionId, DateOnly Date, string? PartnerName, string Notes);

/// <summary>A fight moment pinned to the map, with what the site needs to play it.</summary>
public record MapVideo(
    long Id, string VideoId, string Title, string Url, string ThumbnailUrl, string Channel, DateOnly? UploadDate, int? StartSeconds, string? Note);

/// <param name="Outgoing">Arrows leaving the position: transitions and submissions.</param>
/// <param name="Suggested">Fights of the position's technique from the search, to watch it.</param>
public record NodeDetail(
    MapNode Node, IReadOnlyList<MapEdge> Outgoing, IReadOnlyList<MapNote> Notes, IReadOnlyList<MapVideo> Videos, IReadOnlyList<FightVideo> Suggested);

public record EdgeDetail(MapEdge Edge, IReadOnlyList<MapNote> Notes, IReadOnlyList<MapVideo> Videos, IReadOnlyList<FightVideo> Suggested);

/// <summary>A position to add to the map; an existing one (even hidden) is shown again instead.</summary>
public record NodeInput(string TechniqueId, string? Side, string? Label = null);

/// <param name="Label">Null keeps it, empty goes back to the technique's name.</param>
public record NodeUpdate(string? Label = null, bool? Hidden = null);

public record NodePosition(long Id, double X, double Y);

/// <param name="ToNodeId">Null for submissions.</param>
/// <param name="AgainstMe">Submissions only: the partner finishes the user from the position.</param>
public record EdgeInput(long FromNodeId, long? ToNodeId, string? Kind, string? TechniqueId = null, string? Label = null, bool AgainstMe = false);

/// <param name="Label">Null keeps it, empty clears it.</param>
/// <param name="TechniqueId">Null keeps it, empty clears it.</param>
public record EdgeUpdate(string? Label = null, string? TechniqueId = null);

/// <summary>Pins a fight moment to exactly one position or arrow.</summary>
public record MapVideoInput(long? NodeId, long? EdgeId, string VideoId, int? StartSeconds = null, string? Note = null);

/// <summary>The answer of a map change: what was stored, or why not.</summary>
public record MapResult<T>(SaveStatus Status, T? Value = default, IDictionary<string, string[]>? Errors = null)
{
    public static MapResult<T> Invalid(string field, string message) =>
        new(SaveStatus.Invalid, Errors: new Dictionary<string, string[]> { [field] = [message] });
}
