using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollRadar.Api.Accounts;
using RollRadar.Api.GameMap;
using RollRadar.Api.Models;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;
using RollRadar.Api.Training;

namespace RollRadar.Api.Controllers;

/// <summary>
/// The signed-in user's game map: positions, arrows between them, pinned videos and the numbers counted from the
/// rolls. The user always comes from the session cookie; an id in the address only picks one of the user's own rows.
/// </summary>
[ApiController]
[Route("api/mapa")]
[Authorize(Policy = UserAuthentication.Policy)]
public class GameMapController(IGameMapStore store, IVideoSearchService search, TechniqueCatalog catalog) : ControllerBase
{
    /// <summary>Fights suggested in a position's or arrow's panel.</summary>
    public const int MaxSuggestions = 3;

    private Guid UserId => User.FindUserId() ?? throw new InvalidOperationException("The User policy lets only signed-in users through.");

    [HttpGet]
    public async Task<ActionResult<GameMapView>> Get(CancellationToken cancellationToken) =>
        await store.GetMapAsync(UserId, cancellationToken);

    /// <summary>A position with its numbers, outgoing arrows, latest notes, pinned videos and fights of its technique.</summary>
    [HttpGet("nos/{id:long}")]
    public async Task<ActionResult<NodeDetail>> GetNode(long id, CancellationToken cancellationToken) =>
        await store.GetNodeAsync(UserId, id, cancellationToken) is { } node
            ? node with { Suggested = Suggestions(node.Node.TechniqueId) }
            : NotFound();

    [HttpGet("arestas/{id:long}")]
    public async Task<ActionResult<EdgeDetail>> GetEdge(long id, CancellationToken cancellationToken) =>
        await store.GetEdgeAsync(UserId, id, cancellationToken) is { } edge
            ? edge with { Suggested = Suggestions(edge.Edge.TechniqueId) }
            : NotFound();

    [HttpPost("nos")]
    public async Task<ActionResult<MapNode>> CreateNode(NodeInput input, CancellationToken cancellationToken) =>
        Answer(await store.CreateNodeAsync(UserId, input, cancellationToken));

    /// <summary>Renames (empty label goes back to the technique's name) or hides/shows a position.</summary>
    [HttpPut("nos/{id:long}")]
    public async Task<ActionResult<MapNode>> UpdateNode(long id, NodeUpdate input, CancellationToken cancellationToken) =>
        Answer(await store.UpdateNodeAsync(UserId, id, input, cancellationToken));

    /// <summary>Saves where positions were dragged on the map, several at once.</summary>
    [HttpPut("nos/posicoes")]
    public async Task<IActionResult> MoveNodes(IReadOnlyList<NodePosition> positions, CancellationToken cancellationToken)
    {
        await store.MoveNodesAsync(UserId, positions, cancellationToken);
        return NoContent();
    }

    /// <summary>Only a position never marked in a roll can be deleted (409 otherwise: hide it instead).</summary>
    [HttpDelete("nos/{id:long}")]
    public async Task<IActionResult> DeleteNode(long id, CancellationToken cancellationToken)
    {
        var result = await store.DeleteNodeAsync(UserId, id, cancellationToken);
        return result.Status switch
        {
            SaveStatus.Updated => NoContent(),
            SaveStatus.NotFound => NotFound(),
            _ => Conflict(new ValidationProblemDetails(result.Errors ?? new Dictionary<string, string[]>()) { Status = StatusCodes.Status409Conflict }),
        };
    }

    [HttpPost("arestas")]
    public async Task<ActionResult<MapEdge>> CreateEdge(EdgeInput input, CancellationToken cancellationToken) =>
        Answer(await store.CreateEdgeAsync(UserId, input, cancellationToken));

    [HttpPut("arestas/{id:long}")]
    public async Task<ActionResult<MapEdge>> UpdateEdge(long id, EdgeUpdate input, CancellationToken cancellationToken) =>
        Answer(await store.UpdateEdgeAsync(UserId, id, input, cancellationToken));

    [HttpDelete("arestas/{id:long}")]
    public async Task<IActionResult> DeleteEdge(long id, CancellationToken cancellationToken) =>
        await store.DeleteEdgeAsync(UserId, id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Pins a fight moment (video and second) to one position or one arrow.</summary>
    [HttpPost("videos")]
    public async Task<ActionResult<MapVideo>> AddVideo(MapVideoInput input, CancellationToken cancellationToken) =>
        Answer(await store.AddVideoAsync(UserId, input, cancellationToken));

    [HttpDelete("videos/{id:long}")]
    public async Task<IActionResult> DeleteVideo(long id, CancellationToken cancellationToken) =>
        await store.DeleteVideoAsync(UserId, id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>The site's search for the technique, the fights with most mentions first.</summary>
    private IReadOnlyList<FightVideo> Suggestions(string? techniqueId)
    {
        if (string.IsNullOrEmpty(techniqueId)) return [];
        var technique = catalog.FindById(techniqueId);
        return search.Search(technique?.English ?? techniqueId, null).Videos.Take(MaxSuggestions).ToList();
    }

    /// <summary>201 for something new, 200 for something already there or changed, 404, or 400 with the fields.</summary>
    private ActionResult Answer<T>(MapResult<T> result) => result.Status switch
    {
        SaveStatus.Created => StatusCode(StatusCodes.Status201Created, result.Value),
        SaveStatus.AlreadyExists or SaveStatus.Updated => Ok(result.Value),
        SaveStatus.NotFound => NotFound(),
        _ => ValidationProblem(new ValidationProblemDetails(result.Errors ?? new Dictionary<string, string[]>())
        {
            Title = "Confira os dados do mapa.",
        }),
    };
}
