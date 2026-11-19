using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Accounts;
using RollRadar.Api.Data;
using RollRadar.Api.GameMap;
using RollRadar.Api.Training;

namespace RollRadar.Api.Tests;

/// <summary>The game map over a real database: arrows, pinned videos and the numbers counted from the rolls.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GameMapStoreTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly PositionRef Guard = new("closed-guard", "por_baixo");
    private static readonly PositionRef Mount = new("mount", "por_cima");
    private Guid _ana;
    private Guid _bia;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await TrainingData.SeedTechniquesAsync(database);
        _ana = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "ana", "ana@exemplo.com", "Ana"));
        _bia = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "bia", "bia@exemplo.com", "Bia"));
        await using var db = database.CreateContext();
        db.Videos.Add(new VideoEntity { Id = "v1", Title = "A vs B", Channel = new ChannelEntity { Name = "FloGrappling" } });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<T> Map<T>(Func<DbGameMapStore, Task<T>> action)
    {
        await using var db = database.CreateContext();
        return await action(new DbGameMapStore(db));
    }

    private async Task<SaveResult> Train(Guid user, params RollInput[] rolls)
    {
        await using var db = database.CreateContext();
        return await new DbTrainingStore(db).CreateAsync(user, TrainingData.Session(Today) with { Rolls = rolls });
    }

    private static RollStepInput Step(PositionRef p, string outcome = "neutro", TransitionInput? transition = null) =>
        new(p.TechniqueId, p.Side, outcome, transition);

    private async Task<long> NodeId(Guid user, PositionRef p)
    {
        await using var db = database.CreateContext();
        SnakeCaseEnum.TryParse<NodeSide>(p.Side, out var side);
        return await db.GameNodes.Where(n => n.UserId == user && n.TechniqueId == p.TechniqueId && n.Side == side).Select(n => n.Id).SingleAsync();
    }

    [Fact]
    public async Task A_position_counts_its_steps_outcomes_and_the_submissions_from_it()
    {
        await Train(_ana,
            new RollInput(Steps: [Step(Guard, "bem"), Step(Mount, "bem")], Finishes: [TrainingData.Finalizei("armbar", Mount)]),
            new RollInput(Steps: [Step(Guard, "mal")], Finishes: [TrainingData.FuiFinalizado("rear-naked-choke", Guard), TrainingData.FuiFinalizado()]));
        await Train(_ana, new RollInput(Steps: [Step(Guard)]));

        var map = await Map(s => s.GetMapAsync(_ana));

        var guard = map.Nodes.Single(n => n.TechniqueId == "closed-guard");
        Assert.Equal(new NodeStats(3, 1, 1, 0, 1, 0.5), guard.Stats);
        var mount = map.Nodes.Single(n => n.TechniqueId == "mount");
        Assert.Equal(new NodeStats(1, 1, 0, 1, 0, 1.0), mount.Stats);
    }

    [Fact]
    public async Task Each_submission_with_position_and_technique_becomes_one_arrow_of_the_map()
    {
        await Train(_ana, new RollInput(Finishes: [TrainingData.Finalizei("armbar", Mount), TrainingData.FuiFinalizado("rear-naked-choke", Guard), TrainingData.Finalizei("armbar")]));
        await Train(_ana, new RollInput(Finishes: [TrainingData.Finalizei("armbar", Mount)]));

        var edges = (await Map(s => s.GetMapAsync(_ana))).Edges;

        Assert.Equal(2, edges.Count);
        var armbar = edges.Single(e => e.TechniqueId == "armbar");
        Assert.Equal(("finalizacao", null, false, 2), (armbar.Kind, armbar.ToNodeId, armbar.AgainstMe, armbar.Stats.Uses));
        var choke = edges.Single(e => e.TechniqueId == "rear-naked-choke");
        Assert.Equal((true, await NodeId(_ana, Guard), 1), (choke.AgainstMe, choke.FromNodeId, choke.Stats.Uses));
    }

    [Fact]
    public async Task A_detailed_transition_creates_its_arrow_once_and_counts_the_tries_that_worked()
    {
        var sweep = new TransitionInput(Kind: "raspagem", TechniqueId: "scissor-sweep");
        await Train(_ana, new RollInput(Steps: [Step(Guard, transition: sweep), Step(Mount)]));
        await Train(_ana, new RollInput(Steps: [Step(Guard, transition: sweep with { Succeeded = false }), Step(Mount)]));

        var edge = Assert.Single((await Map(s => s.GetMapAsync(_ana))).Edges);
        Assert.Equal(("raspagem", "scissor-sweep", await NodeId(_ana, Guard), await NodeId(_ana, Mount)), (edge.Kind, edge.TechniqueId, edge.FromNodeId, edge.ToNodeId));
        Assert.Equal(new EdgeStats(2, 1, 0.5), edge.Stats);

        // Chosen by id the next time, and the step remembers it.
        var third = await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(EdgeId: edge.Id)), Step(Mount)]));
        Assert.Equal((edge.Id, true), (third.Session!.Rolls[0].Steps[0].EdgeId, third.Session.Rolls[0].Steps[0].EdgeSucceeded));
        Assert.Equal(3, Assert.Single((await Map(s => s.GetMapAsync(_ana))).Edges).Stats.Uses);
    }

    [Fact]
    public async Task A_transition_must_suit_its_kind_and_be_an_arrow_of_the_user_leaving_that_position()
    {
        var wrong = await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "raspagem", TechniqueId: "knee-cut")), Step(Mount)]));
        Assert.Equal(["rolls[0].steps[0].transition.techniqueId"], wrong.Errors!.Keys);
        var finish = await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "finalizacao")), Step(Mount)]));
        Assert.Equal(["rolls[0].steps[0].transition.kind"], finish.Errors!.Keys);

        await Train(_bia, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "passagem")), Step(Mount)]));
        var hers = Assert.Single((await Map(s => s.GetMapAsync(_bia))).Edges);
        var stolen = await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(EdgeId: hers.Id)), Step(Mount)]));
        Assert.Equal(["rolls[0].steps[0].transition.edgeId"], stolen.Errors!.Keys);
    }

    [Fact]
    public async Task Arrows_are_checked_against_their_kind()
    {
        await Train(_ana, new RollInput(Steps: [Step(Guard), Step(Mount)]));
        var guard = await NodeId(_ana, Guard);
        var mount = await NodeId(_ana, Mount);

        async Task<IEnumerable<string>> Problems(EdgeInput input) => (await Map(s => s.CreateEdgeAsync(_ana, input))).Errors?.Keys ?? [];

        Assert.Equal(["techniqueId"], await Problems(new EdgeInput(guard, mount, "raspagem", "knee-cut")));
        Assert.Equal(["techniqueId"], await Problems(new EdgeInput(guard, null, "finalizacao", "scissor-sweep")));
        Assert.Equal(["toNodeId"], await Problems(new EdgeInput(guard, mount, "finalizacao", "armbar")));
        Assert.Equal(["toNodeId"], await Problems(new EdgeInput(guard, null, "passagem")));
        Assert.Equal(["toNodeId"], await Problems(new EdgeInput(guard, guard, "transicao")));
        Assert.Equal(["againstMe"], await Problems(new EdgeInput(guard, mount, "raspagem", AgainstMe: true)));
        Assert.Equal(["kind"], await Problems(new EdgeInput(guard, mount, "voadora")));

        var created = await Map(s => s.CreateEdgeAsync(_ana, new EdgeInput(guard, mount, "raspagem", "scissor-sweep")));
        Assert.Equal(SaveStatus.Created, created.Status);
        var again = await Map(s => s.CreateEdgeAsync(_ana, new EdgeInput(guard, mount, "raspagem", "scissor-sweep")));
        Assert.Equal((SaveStatus.AlreadyExists, created.Value!.Id), (again.Status, again.Value!.Id));
        Assert.Equal(SaveStatus.Created, (await Map(s => s.CreateEdgeAsync(_ana, new EdgeInput(guard, null, "finalizacao", "armbar", AgainstMe: true)))).Status);
    }

    [Fact]
    public async Task Highlights_need_five_occurrences_and_weak_points_count_submissions_suffered()
    {
        // Guard: 4 "fui bem" + 1 "sofri" (5 occurrences): strong. Mount: 1 "sofri" + 3 submissions suffered from it (4): too few.
        await Train(_ana,
            new RollInput(Steps: [Step(Guard, "bem"), Step(Mount, "mal")]),
            new RollInput(Steps: [Step(Guard, "bem")], Finishes: [TrainingData.FuiFinalizado("armbar", Mount)]),
            new RollInput(Steps: [Step(Guard, "bem")], Finishes: [TrainingData.FuiFinalizado("armbar", Mount)]),
            new RollInput(Steps: [Step(Guard, "bem"), Step(Guard, "mal")], Finishes: [TrainingData.FuiFinalizado("armbar", Mount)]));

        var map = await Map(s => s.GetMapAsync(_ana));
        var strong = Assert.Single(map.Highlights.Strong);
        Assert.Equal(("node", await NodeId(_ana, Guard), 0.8, 5), (strong.Target, strong.Id, strong.Score, strong.Occurrences));
        Assert.Empty(map.Highlights.Weak);

        await Train(_ana, new RollInput(Steps: [Step(Mount)]));
        var weak = Assert.Single((await Map(s => s.GetMapAsync(_ana))).Highlights.Weak);
        Assert.Equal((await NodeId(_ana, Mount), 1.0, 5), (weak.Id, weak.Score, weak.Occurrences));
    }

    [Fact]
    public async Task A_panel_has_the_latest_five_notes_and_the_pinned_videos()
    {
        for (var i = 0; i < 6; i++)
        {
            await using var db = database.CreateContext();
            await new DbTrainingStore(db).CreateAsync(_ana, TrainingData.Session(Today.AddDays(-i)) with
            {
                Rolls = [new RollInput($"Parceiro {i}", Notes: $"nota {i}", Steps: [Step(Guard)])],
            });
        }
        var guard = await NodeId(_ana, Guard);
        var pinned = await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(guard, null, "v1", 95, "olha a pegada")));
        Assert.Equal(SaveStatus.Created, pinned.Status);

        var detail = (await Map(s => s.GetNodeAsync(_ana, guard)))!;

        Assert.Equal(["nota 0", "nota 1", "nota 2", "nota 3", "nota 4"], detail.Notes.Select(n => n.Notes));
        Assert.Equal(("Parceiro 0", Today), (detail.Notes[0].PartnerName, detail.Notes[0].Date));
        var video = Assert.Single(detail.Videos);
        Assert.Equal(("A vs B", "FloGrappling", 95, "olha a pegada"), (video.Title, video.Channel, video.StartSeconds, video.Note));
        Assert.Empty(detail.Suggested);
    }

    [Fact]
    public async Task A_video_goes_to_exactly_one_position_or_arrow()
    {
        await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "transicao")), Step(Mount)]));
        var guard = await NodeId(_ana, Guard);
        var edge = Assert.Single((await Map(s => s.GetMapAsync(_ana))).Edges).Id;

        Assert.Equal(["target"], (await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(null, null, "v1"))))!.Errors!.Keys);
        Assert.Equal(["target"], (await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(guard, edge, "v1"))))!.Errors!.Keys);
        Assert.Equal(["videoId"], (await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(guard, null, "nao-existe"))))!.Errors!.Keys);
        Assert.Equal(SaveStatus.Created, (await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(null, edge, "v1")))).Status);

        // The database refuses it too.
        await using var db = database.CreateContext();
        db.MapVideos.Add(new MapVideoEntity { UserId = _ana, NodeId = guard, EdgeId = edge, VideoId = "v1", CreatedAt = DateTime.UtcNow });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task A_user_never_reads_or_changes_someone_elses_map()
    {
        await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "transicao")), Step(Mount)]));
        var guard = await NodeId(_ana, Guard);
        var edge = Assert.Single((await Map(s => s.GetMapAsync(_ana))).Edges).Id;
        var video = (await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(guard, null, "v1")))).Value!.Id;

        Assert.Empty((await Map(s => s.GetMapAsync(_bia))).Nodes);
        Assert.Null(await Map(s => s.GetNodeAsync(_bia, guard)));
        Assert.Null(await Map(s => s.GetEdgeAsync(_bia, edge)));
        Assert.Equal(SaveStatus.NotFound, (await Map(s => s.UpdateNodeAsync(_bia, guard, new NodeUpdate("meu")))).Status);
        Assert.Equal(SaveStatus.NotFound, (await Map(s => s.DeleteNodeAsync(_bia, guard))).Status);
        Assert.Equal(0, await Map(s => s.MoveNodesAsync(_bia, [new NodePosition(guard, 1, 2)])));
        Assert.False(await Map(s => s.DeleteEdgeAsync(_bia, edge)));
        Assert.False(await Map(s => s.DeleteVideoAsync(_bia, video)));
        Assert.Equal(["nodeId"], (await Map(s => s.AddVideoAsync(_bia, new MapVideoInput(guard, null, "v1")))).Errors!.Keys);
        Assert.Equal(["fromNodeId"], (await Map(s => s.CreateEdgeAsync(_bia, new EdgeInput(guard, null, "finalizacao")))).Errors!.Keys);

        var node = (await Map(s => s.GetMapAsync(_ana))).Nodes.Single(n => n.Id == guard);
        Assert.Equal((null, false, null), (node.Label, node.Hidden, node.X));
    }

    [Fact]
    public async Task Positions_are_added_moved_renamed_hidden_and_deleted_only_when_unused()
    {
        await Train(_ana, new RollInput(Steps: [Step(Guard)]));
        var guard = await NodeId(_ana, Guard);

        var added = await Map(s => s.CreateNodeAsync(_ana, new NodeInput("mount", "por_cima")));
        Assert.Equal(SaveStatus.Created, added.Status);
        Assert.Equal(["techniqueId"], (await Map(s => s.CreateNodeAsync(_ana, new NodeInput("armbar", "por_cima")))).Errors!.Keys);
        Assert.Equal(1, await Map(s => s.MoveNodesAsync(_ana, [new NodePosition(guard, 120.5, -40)])));
        await Map(s => s.UpdateNodeAsync(_ana, guard, new NodeUpdate("Minha guarda", Hidden: true)));

        var node = (await Map(s => s.GetMapAsync(_ana))).Nodes.Single(n => n.Id == guard);
        Assert.Equal(("Minha guarda", true, 120.5, -40.0), (node.Label, node.Hidden, node.X, node.Y));
        // Adding it again shows it.
        Assert.Equal(SaveStatus.AlreadyExists, (await Map(s => s.CreateNodeAsync(_ana, new NodeInput("closed-guard", "por_baixo")))).Status);
        Assert.False((await Map(s => s.GetMapAsync(_ana))).Nodes.Single(n => n.Id == guard).Hidden);

        Assert.Equal(SaveStatus.Conflict, (await Map(s => s.DeleteNodeAsync(_ana, guard))).Status);
        Assert.Equal(SaveStatus.Updated, (await Map(s => s.DeleteNodeAsync(_ana, added.Value!.Id))).Status);
    }

    [Fact]
    public async Task The_export_has_the_map_and_deleting_the_account_removes_it()
    {
        await Train(_ana, new RollInput(Steps: [Step(Guard, transition: new TransitionInput(Kind: "transicao")), Step(Mount)]));
        var guard = await NodeId(_ana, Guard);
        await Map(s => s.MoveNodesAsync(_ana, [new NodePosition(guard, 10, 20)]));
        await Map(s => s.AddVideoAsync(_ana, new MapVideoInput(guard, null, "v1", 30)));

        await using (var db = database.CreateContext())
        {
            var export = (await new DbTrainingStore(db).ExportAsync(_ana))!;
            Assert.Equal("transicao", Assert.Single(export.GameEdges).Kind);
            Assert.Equal(("v1", 30), (Assert.Single(export.MapVideos).VideoId, export.MapVideos[0].StartSeconds));
            Assert.Equal((10.0, 20.0), (export.GameNodes.Single(n => n.Id == guard).X, export.GameNodes.Single(n => n.Id == guard).Y));
        }

        await new DbAccountStore(database.CreateContext()).DeleteAsync(_ana);
        await using var check = database.CreateContext();
        Assert.Equal((0, 0, 0), (await check.GameEdges.CountAsync(), await check.MapVideos.CountAsync(), await check.GameNodes.CountAsync()));
    }
}
