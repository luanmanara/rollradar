using Microsoft.EntityFrameworkCore;
using RollRadar.Api.GameMap;
using RollRadar.Api.Training;

namespace RollRadar.Api.Data;

/// <summary><see cref="IGameMapStore"/> on game_nodes, game_edges and map_videos; numbers are counted from the rolls.</summary>
public class DbGameMapStore(RollRadarDbContext db) : IGameMapStore
{
    public async Task<GameMapView> GetMapAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var (steps, finishes) = await FactsAsync(userId, cancellationToken);
        var nodes = (await db.GameNodes.AsNoTracking().Where(n => n.UserId == userId).OrderBy(n => n.Id).ToListAsync(cancellationToken))
            .Select(n => ToModel(n, steps, finishes)).ToList();
        var edges = (await db.GameEdges.AsNoTracking().Where(e => e.UserId == userId).OrderBy(e => e.Id).ToListAsync(cancellationToken))
            .Select(e => ToModel(e, steps, finishes)).ToList();
        return new GameMapView(nodes, edges, GameMapStats.Highlights(nodes, edges));
    }

    public async Task<NodeDetail?> GetNodeAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        var node = await db.GameNodes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (node is null) return null;

        var (steps, finishes) = await FactsAsync(userId, cancellationToken);
        var outgoing = await db.GameEdges.AsNoTracking().Where(e => e.UserId == userId && e.FromNodeId == id).OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);
        return new NodeDetail(
            ToModel(node, steps, finishes),
            outgoing.Select(e => ToModel(e, steps, finishes)).ToList(),
            GameMapStats.Notes(GameMapStats.RollsOfNode(id, steps, finishes), await NotesAsync(userId, cancellationToken)),
            await VideosAsync(db.MapVideos.Where(v => v.UserId == userId && v.NodeId == id), cancellationToken),
            []);
    }

    public async Task<EdgeDetail?> GetEdgeAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        var edge = await db.GameEdges.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (edge is null) return null;

        var (steps, finishes) = await FactsAsync(userId, cancellationToken);
        return new EdgeDetail(
            ToModel(edge, steps, finishes),
            GameMapStats.Notes(GameMapStats.RollsOfEdge(edge, steps, finishes), await NotesAsync(userId, cancellationToken)),
            await VideosAsync(db.MapVideos.Where(v => v.UserId == userId && v.EdgeId == id), cancellationToken),
            []);
    }

    public async Task<MapResult<MapNode>> CreateNodeAsync(Guid userId, NodeInput input, CancellationToken cancellationToken = default)
    {
        if (!SnakeCaseEnum.TryParse<NodeSide>(input.Side, out var side))
            return MapResult<MapNode>.Invalid("side", "Escolha por cima, por baixo ou neutro.");
        var category = await db.Techniques.Where(t => t.Id == input.TechniqueId).Select(t => t.Category).FirstOrDefaultAsync(cancellationToken);
        if (category is null || !TrainingValidation.PositionCategories.Contains(category))
            return MapResult<MapNode>.Invalid("techniqueId", "A posição deve ser do catálogo de posições ou guardas.");
        if (Clean(input.Label)?.Length > GameMapRules.MaxLabelLength)
            return MapResult<MapNode>.Invalid("label", $"Use no máximo {GameMapRules.MaxLabelLength} caracteres.");

        var node = await db.GameNodes.FirstOrDefaultAsync(n => n.UserId == userId && n.TechniqueId == input.TechniqueId && n.Side == side, cancellationToken);
        var status = SaveStatus.AlreadyExists;
        if (node is null)
        {
            node = new GameNodeEntity { UserId = userId, TechniqueId = input.TechniqueId, Side = side, CreatedAt = DateTime.UtcNow };
            db.GameNodes.Add(node);
            status = SaveStatus.Created;
        }
        node.Hidden = false;
        if (Clean(input.Label) is { } label) node.Label = label;
        await db.SaveChangesAsync(cancellationToken);
        return new MapResult<MapNode>(status, await NodeModelAsync(userId, node, cancellationToken));
    }

    public async Task<MapResult<MapNode>> UpdateNodeAsync(Guid userId, long id, NodeUpdate input, CancellationToken cancellationToken = default)
    {
        var node = await db.GameNodes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (node is null) return new MapResult<MapNode>(SaveStatus.NotFound);
        if (input.Label?.Trim().Length > GameMapRules.MaxLabelLength)
            return MapResult<MapNode>.Invalid("label", $"Use no máximo {GameMapRules.MaxLabelLength} caracteres.");

        if (input.Label is not null) node.Label = Clean(input.Label);
        if (input.Hidden is { } hidden) node.Hidden = hidden;
        await db.SaveChangesAsync(cancellationToken);
        return new MapResult<MapNode>(SaveStatus.Updated, await NodeModelAsync(userId, node, cancellationToken));
    }

    public async Task<MapResult<bool>> DeleteNodeAsync(Guid userId, long id, CancellationToken cancellationToken = default)
    {
        var node = await db.GameNodes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (node is null) return new MapResult<bool>(SaveStatus.NotFound);
        if (await db.RollSteps.AnyAsync(s => s.NodeId == id, cancellationToken) || await db.RollFinishes.AnyAsync(f => f.NodeId == id, cancellationToken))
            return new MapResult<bool>(SaveStatus.Conflict, Errors: new Dictionary<string, string[]>
            {
                ["id"] = ["Esta posição aparece nos seus rolas; esconda-a em vez de apagar."],
            });

        db.GameNodes.Remove(node);
        await db.SaveChangesAsync(cancellationToken);
        return new MapResult<bool>(SaveStatus.Updated, true);
    }

    public async Task<int> MoveNodesAsync(Guid userId, IReadOnlyList<NodePosition> positions, CancellationToken cancellationToken = default)
    {
        var ids = positions.Select(p => p.Id).ToList();
        var nodes = await db.GameNodes.Where(n => n.UserId == userId && ids.Contains(n.Id)).ToListAsync(cancellationToken);
        foreach (var node in nodes)
        {
            var position = positions.Last(p => p.Id == node.Id);
            if (!double.IsFinite(position.X) || !double.IsFinite(position.Y)) continue;
            node.X = position.X;
            node.Y = position.Y;
        }
        await db.SaveChangesAsync(cancellationToken);
        return nodes.Count;
    }

    public async Task<MapResult<MapEdge>> CreateEdgeAsync(Guid userId, EdgeInput input, CancellationToken cancellationToken = default)
    {
        if (!SnakeCaseEnum.TryParse<EdgeKind>(input.Kind, out var kind))
            return MapResult<MapEdge>.Invalid("kind", "Escolha raspagem, passagem, fuga, queda, transição ou finalização.");
        var nodeIds = new[] { input.FromNodeId, input.ToNodeId ?? input.FromNodeId };
        var known = await db.GameNodes.Where(n => n.UserId == userId && nodeIds.Contains(n.Id)).Select(n => n.Id).ToListAsync(cancellationToken);
        if (!known.Contains(input.FromNodeId)) return MapResult<MapEdge>.Invalid("fromNodeId", "Posição de origem não encontrada.");

        if (kind == EdgeKind.Finalizacao)
        {
            if (input.ToNodeId is not null) return MapResult<MapEdge>.Invalid("toNodeId", "Uma finalização não leva a outra posição.");
        }
        else
        {
            if (input.ToNodeId is not { } to || !known.Contains(to)) return MapResult<MapEdge>.Invalid("toNodeId", "Escolha a posição de destino.");
            if (to == input.FromNodeId) return MapResult<MapEdge>.Invalid("toNodeId", "O destino deve ser outra posição.");
            if (input.AgainstMe) return MapResult<MapEdge>.Invalid("againstMe", "Só uma finalização pode ser sofrida.");
        }
        var techniqueId = Clean(input.TechniqueId);
        if (GameMapRules.CheckTechnique(kind, techniqueId, await CategoriesAsync(cancellationToken)) is { } problem)
            return MapResult<MapEdge>.Invalid("techniqueId", problem);
        if (Clean(input.Label)?.Length > GameMapRules.MaxLabelLength)
            return MapResult<MapEdge>.Invalid("label", $"Use no máximo {GameMapRules.MaxLabelLength} caracteres.");

        var edge = await db.GameEdges.FirstOrDefaultAsync(e => e.UserId == userId && e.FromNodeId == input.FromNodeId
            && e.ToNodeId == input.ToNodeId && e.Kind == kind && e.TechniqueId == techniqueId && e.AgainstMe == input.AgainstMe, cancellationToken);
        if (edge is not null) return new MapResult<MapEdge>(SaveStatus.AlreadyExists, await EdgeModelAsync(userId, edge, cancellationToken));

        edge = new GameEdgeEntity
        {
            UserId = userId, FromNodeId = input.FromNodeId, ToNodeId = input.ToNodeId, Kind = kind, TechniqueId = techniqueId,
            Label = Clean(input.Label), AgainstMe = input.AgainstMe, CreatedAt = DateTime.UtcNow,
        };
        db.GameEdges.Add(edge);
        await db.SaveChangesAsync(cancellationToken);
        return new MapResult<MapEdge>(SaveStatus.Created, await EdgeModelAsync(userId, edge, cancellationToken));
    }

    public async Task<MapResult<MapEdge>> UpdateEdgeAsync(Guid userId, long id, EdgeUpdate input, CancellationToken cancellationToken = default)
    {
        var edge = await db.GameEdges.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (edge is null) return new MapResult<MapEdge>(SaveStatus.NotFound);
        if (input.Label?.Trim().Length > GameMapRules.MaxLabelLength)
            return MapResult<MapEdge>.Invalid("label", $"Use no máximo {GameMapRules.MaxLabelLength} caracteres.");
        if (input.TechniqueId is not null
            && GameMapRules.CheckTechnique(edge.Kind, Clean(input.TechniqueId), await CategoriesAsync(cancellationToken)) is { } problem)
            return MapResult<MapEdge>.Invalid("techniqueId", problem);

        if (input.Label is not null) edge.Label = Clean(input.Label);
        if (input.TechniqueId is not null) edge.TechniqueId = Clean(input.TechniqueId);
        await db.SaveChangesAsync(cancellationToken);
        return new MapResult<MapEdge>(SaveStatus.Updated, await EdgeModelAsync(userId, edge, cancellationToken));
    }

    public async Task<bool> DeleteEdgeAsync(Guid userId, long id, CancellationToken cancellationToken = default) =>
        await db.GameEdges.Where(e => e.Id == id && e.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<MapResult<MapVideo>> AddVideoAsync(Guid userId, MapVideoInput input, CancellationToken cancellationToken = default)
    {
        if ((input.NodeId is null) == (input.EdgeId is null))
            return MapResult<MapVideo>.Invalid("target", "Anexe o vídeo a uma posição ou a uma seta, só uma.");
        if (input.NodeId is { } nodeId && !await db.GameNodes.AnyAsync(n => n.Id == nodeId && n.UserId == userId, cancellationToken))
            return MapResult<MapVideo>.Invalid("nodeId", "Posição não encontrada.");
        if (input.EdgeId is { } edgeId && !await db.GameEdges.AnyAsync(e => e.Id == edgeId && e.UserId == userId, cancellationToken))
            return MapResult<MapVideo>.Invalid("edgeId", "Seta não encontrada.");
        if (!await db.Videos.AnyAsync(v => v.Id == input.VideoId, cancellationToken))
            return MapResult<MapVideo>.Invalid("videoId", "Vídeo não encontrado.");
        if (input.StartSeconds is < 0) return MapResult<MapVideo>.Invalid("startSeconds", "Segundo inválido.");
        if (input.Note?.Length > 500) return MapResult<MapVideo>.Invalid("note", "Use no máximo 500 caracteres.");

        var video = new MapVideoEntity
        {
            UserId = userId, NodeId = input.NodeId, EdgeId = input.EdgeId, VideoId = input.VideoId, StartSeconds = input.StartSeconds,
            Note = Clean(input.Note), CreatedAt = DateTime.UtcNow,
        };
        db.MapVideos.Add(video);
        await db.SaveChangesAsync(cancellationToken);
        var stored = await VideosAsync(db.MapVideos.Where(v => v.Id == video.Id), cancellationToken);
        return new MapResult<MapVideo>(SaveStatus.Created, stored.Single());
    }

    public async Task<bool> DeleteVideoAsync(Guid userId, long id, CancellationToken cancellationToken = default) =>
        await db.MapVideos.Where(v => v.Id == id && v.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Every step and submission of the user's rolls, which the numbers are counted from.</summary>
    private async Task<(List<StepFact> Steps, List<FinishFact> Finishes)> FactsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var steps = await db.RollSteps.AsNoTracking().Where(s => s.Roll!.Session!.UserId == userId)
            .Select(s => new StepFact(s.RollId, s.NodeId, s.Outcome, s.EdgeId, s.EdgeSucceeded)).ToListAsync(cancellationToken);
        var finishes = await db.RollFinishes.AsNoTracking().Where(f => f.Roll!.Session!.UserId == userId)
            .Select(f => new FinishFact(f.RollId, f.NodeId, f.ByMe, f.TechniqueId)).ToListAsync(cancellationToken);
        return (steps, finishes);
    }

    private async Task<Dictionary<long, RollNoteFact>> NotesAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Rolls.AsNoTracking().Where(r => r.Session!.UserId == userId && r.Notes != null)
            .Select(r => new RollNoteFact(r.Id, r.SessionId, r.Session!.Date, r.Partner != null ? r.Partner.Name : null, r.Notes))
            .ToDictionaryAsync(r => r.RollId, cancellationToken);

    private async Task<List<MapVideo>> VideosAsync(IQueryable<MapVideoEntity> query, CancellationToken cancellationToken) =>
        await query.AsNoTracking().OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new MapVideo(v.Id, v.VideoId, v.Video!.Title, $"https://www.youtube.com/watch?v={v.VideoId}",
                $"https://i.ytimg.com/vi/{v.VideoId}/mqdefault.jpg", v.Video.Channel != null ? v.Video.Channel.Name : "", v.Video.UploadDate,
                v.StartSeconds, v.Note))
            .ToListAsync(cancellationToken);

    private async Task<MapNode> NodeModelAsync(Guid userId, GameNodeEntity node, CancellationToken cancellationToken)
    {
        var (steps, finishes) = await FactsAsync(userId, cancellationToken);
        return ToModel(node, steps, finishes);
    }

    private async Task<MapEdge> EdgeModelAsync(Guid userId, GameEdgeEntity edge, CancellationToken cancellationToken)
    {
        var (steps, finishes) = await FactsAsync(userId, cancellationToken);
        return ToModel(edge, steps, finishes);
    }

    private Task<Dictionary<string, string>> CategoriesAsync(CancellationToken cancellationToken) =>
        db.Techniques.AsNoTracking().ToDictionaryAsync(t => t.Id, t => t.Category, cancellationToken);

    private static MapNode ToModel(GameNodeEntity n, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes) =>
        new(n.Id, n.TechniqueId, n.Label, SnakeCaseEnum.Name(n.Side), n.Hidden, n.X, n.Y, GameMapStats.ForNode(n.Id, steps, finishes));

    private static MapEdge ToModel(GameEdgeEntity e, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes) =>
        new(e.Id, e.FromNodeId, e.ToNodeId, SnakeCaseEnum.Name(e.Kind), e.TechniqueId, e.Label, e.AgainstMe,
            GameMapStats.ForEdge(e, steps, finishes));

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
