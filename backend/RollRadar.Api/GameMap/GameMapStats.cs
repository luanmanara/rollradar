using RollRadar.Api.Data;

namespace RollRadar.Api.GameMap;

/// <summary>A position marked in a roll, as the statistics read it.</summary>
public record StepFact(long RollId, long NodeId, StepOutcome Outcome, long? EdgeId, bool? EdgeSucceeded);

/// <summary>A submission of a roll, as the statistics read it.</summary>
public record FinishFact(long RollId, long? NodeId, bool ByMe, string? TechniqueId);

/// <summary>A roll's notes and where they come from.</summary>
public record RollNoteFact(long RollId, Guid SessionId, DateOnly Date, string? PartnerName, string? Notes);

/// <summary>
/// The numbers of the map, counted from the rolls (no stored totals, no AI). A position counts its steps, "fui bem",
/// "sofri" and the submissions that came from it; a transition its tries in detailed rolls and how many worked; a
/// submission arrow how many times it happened.
/// </summary>
public static class GameMapStats
{
    /// <summary>A position or transition needs at least this many occurrences to be a strong or weak point.</summary>
    public const int MinOccurrences = 5;

    public const int MaxHighlights = 3;
    public const int MaxNotes = 5;

    public static NodeStats ForNode(long nodeId, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes)
    {
        var mine = steps.Where(s => s.NodeId == nodeId).ToList();
        var good = mine.Count(s => s.Outcome == StepOutcome.Bem);
        var bad = mine.Count(s => s.Outcome == StepOutcome.Mal);
        var from = finishes.Where(f => f.NodeId == nodeId).ToList();
        return new NodeStats(mine.Count, good, bad, from.Count(f => f.ByMe), from.Count(f => !f.ByMe),
            good + bad > 0 ? (double)good / (good + bad) : null);
    }

    public static EdgeStats ForEdge(GameEdgeEntity edge, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes)
    {
        if (edge.Kind == EdgeKind.Finalizacao)
            return new EdgeStats(finishes.Count(f => IsFinishOf(f, edge)), 0, null);

        var tries = steps.Where(s => s.EdgeId == edge.Id).ToList();
        var worked = tries.Count(s => s.EdgeSucceeded == true);
        return new EdgeStats(tries.Count, worked, tries.Count > 0 ? (double)worked / tries.Count : null);
    }

    /// <summary>
    /// Up to three strong and three weak points among positions and transitions with enough occurrences. A position's
    /// good outcomes are "fui bem" and the submissions it gave; the bad ones "sofri" and the submissions suffered there.
    /// Exactly even items are neither.
    /// </summary>
    public static Highlights Highlights(IReadOnlyList<MapNode> nodes, IReadOnlyList<MapEdge> edges)
    {
        var candidates = new List<(string Target, long Id, int Good, int Bad, int Occurrences)>();
        foreach (var n in nodes.Where(n => !n.Hidden))
        {
            var s = n.Stats;
            candidates.Add(("node", n.Id, s.Good + s.FinishesFor, s.Bad + s.FinishesAgainst, s.Steps + s.FinishesFor + s.FinishesAgainst));
        }
        foreach (var e in edges.Where(e => e.Kind != SnakeCaseEnum.Name(EdgeKind.Finalizacao)))
            candidates.Add(("edge", e.Id, e.Stats.Succeeded, e.Stats.Uses - e.Stats.Succeeded, e.Stats.Uses));

        var scored = candidates
            .Where(c => c.Occurrences >= MinOccurrences && c.Good + c.Bad > 0)
            .Select(c => (c.Target, c.Id, Good: (double)c.Good / (c.Good + c.Bad), Bad: (double)c.Bad / (c.Good + c.Bad), c.Occurrences))
            .ToList();
        return new Highlights(
            scored.Where(c => c.Good > 0.5).OrderByDescending(c => c.Good).ThenByDescending(c => c.Occurrences).ThenBy(c => c.Id)
                .Take(MaxHighlights).Select(c => new Highlight(c.Target, c.Id, c.Good, c.Occurrences)).ToList(),
            scored.Where(c => c.Bad > 0.5).OrderByDescending(c => c.Bad).ThenByDescending(c => c.Occurrences).ThenBy(c => c.Id)
                .Take(MaxHighlights).Select(c => new Highlight(c.Target, c.Id, c.Bad, c.Occurrences)).ToList());
    }

    /// <summary>The latest notes of the rolls in <paramref name="rollIds"/>, newest session first.</summary>
    public static IReadOnlyList<MapNote> Notes(IEnumerable<long> rollIds, IReadOnlyDictionary<long, RollNoteFact> rolls)
    {
        return rollIds.Distinct()
            .Select(id => rolls.GetValueOrDefault(id))
            .Where(r => r is { Notes: not null and not "" })
            .OrderByDescending(r => r!.Date).ThenByDescending(r => r!.RollId)
            .Take(MaxNotes)
            .Select(r => new MapNote(r!.SessionId, r.Date, r.PartnerName, r.Notes!))
            .ToList();
    }

    /// <summary>Rolls that went through the position: a step on it or a submission from it.</summary>
    public static IEnumerable<long> RollsOfNode(long nodeId, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes) =>
        steps.Where(s => s.NodeId == nodeId).Select(s => s.RollId).Concat(finishes.Where(f => f.NodeId == nodeId).Select(f => f.RollId));

    /// <summary>Rolls that used the arrow: a detailed step through it, or the submission it stands for.</summary>
    public static IEnumerable<long> RollsOfEdge(GameEdgeEntity edge, IReadOnlyList<StepFact> steps, IReadOnlyList<FinishFact> finishes) =>
        edge.Kind == EdgeKind.Finalizacao
            ? finishes.Where(f => IsFinishOf(f, edge)).Select(f => f.RollId)
            : steps.Where(s => s.EdgeId == edge.Id).Select(s => s.RollId);

    /// <summary>A submission arrow stands for the roll finishes from its position, with its technique, in its direction.</summary>
    private static bool IsFinishOf(FinishFact finish, GameEdgeEntity edge) =>
        finish.NodeId == edge.FromNodeId && finish.TechniqueId == edge.TechniqueId && finish.ByMe == !edge.AgainstMe;
}
