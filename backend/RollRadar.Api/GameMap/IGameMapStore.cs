namespace RollRadar.Api.GameMap;

/// <summary>
/// The game map of each user: positions, arrows, pinned videos and the numbers counted from the rolls. Every method
/// takes the signed-in user's id (from the cookie) and only sees that user's rows; someone else's id answers not found.
/// </summary>
public interface IGameMapStore
{
    Task<GameMapView> GetMapAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The position with its arrows, notes and videos; <see cref="NodeDetail.Suggested"/> is left empty for the caller.</summary>
    Task<NodeDetail?> GetNodeAsync(Guid userId, long id, CancellationToken cancellationToken = default);

    /// <summary>The arrow with its notes and videos; <see cref="EdgeDetail.Suggested"/> is left empty for the caller.</summary>
    Task<EdgeDetail?> GetEdgeAsync(Guid userId, long id, CancellationToken cancellationToken = default);

    /// <summary>Adds a position, or shows again the one already there for that technique and side.</summary>
    Task<MapResult<MapNode>> CreateNodeAsync(Guid userId, NodeInput input, CancellationToken cancellationToken = default);

    Task<MapResult<MapNode>> UpdateNodeAsync(Guid userId, long id, NodeUpdate input, CancellationToken cancellationToken = default);

    /// <summary>Only positions never marked in a roll can go (the history would go with them); hide the others.</summary>
    Task<MapResult<bool>> DeleteNodeAsync(Guid userId, long id, CancellationToken cancellationToken = default);

    /// <summary>Saves where the user dragged positions; ids that are not the user's are ignored. Returns how many moved.</summary>
    Task<int> MoveNodesAsync(Guid userId, IReadOnlyList<NodePosition> positions, CancellationToken cancellationToken = default);

    /// <summary>Adds an arrow, or returns the identical one already there (<see cref="Training.SaveStatus.AlreadyExists"/>).</summary>
    Task<MapResult<MapEdge>> CreateEdgeAsync(Guid userId, EdgeInput input, CancellationToken cancellationToken = default);

    Task<MapResult<MapEdge>> UpdateEdgeAsync(Guid userId, long id, EdgeUpdate input, CancellationToken cancellationToken = default);

    Task<bool> DeleteEdgeAsync(Guid userId, long id, CancellationToken cancellationToken = default);

    Task<MapResult<MapVideo>> AddVideoAsync(Guid userId, MapVideoInput input, CancellationToken cancellationToken = default);

    Task<bool> DeleteVideoAsync(Guid userId, long id, CancellationToken cancellationToken = default);
}
