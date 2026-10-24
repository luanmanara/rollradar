using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Accounts;
using RollRadar.Api.Data;
using RollRadar.Api.Training;

namespace RollRadar.Api.Tests;

/// <summary>The training diary over a real database: each user only ever sees their own rows.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TrainingStoreTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private Guid _ana;
    private Guid _bia;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await TrainingData.SeedTechniquesAsync(database);
        _ana = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "ana", "ana@exemplo.com", "Ana"));
        _bia = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "bia", "bia@exemplo.com", "Bia"));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A new context per call, like one request each in the API.</summary>
    private async Task<T> With<T>(Func<DbTrainingStore, Task<T>> action)
    {
        await using var db = database.CreateContext();
        return await action(new DbTrainingStore(db));
    }

    [Fact]
    public async Task A_session_is_stored_with_its_rolls_tags_and_point_to_fix()
    {
        var input = TrainingData.Session(Today) with
        {
            TechniqueNotes = "Raspagem de gancho",
            Techniques = ["armbar", "closed-guard"],
            Sleep = 4, Pain = 2, Energy = 5,
            FocusPoint = "  Postura na guarda fechada  ",
            Rolls =
            [
                new RollInput("João", "azul", 5, "Bom rola",
                    [new RollStepInput("closed-guard", "por_baixo", "bem"), new RollStepInput("mount", "por_cima")],
                    [TrainingData.Finalizei("armbar", new PositionRef("mount", "por_cima")), TrainingData.FuiFinalizado(), TrainingData.Finalizei()]),
                new RollInput(),
            ],
        };

        var result = await With(s => s.CreateAsync(_ana, input));

        Assert.Equal(SaveStatus.Created, result.Status);
        var session = (await With(s => s.GetAsync(_ana, input.Id)))!;
        Assert.Equal(("no_gi", "aula", 90), (session.Attire, session.Format, session.DurationMinutes));
        Assert.Equal(["armbar", "closed-guard"], session.Techniques);
        Assert.Equal((4, 2, 5), (session.Sleep, session.Pain, session.Energy));
        Assert.Equal("Postura na guarda fechada", session.FocusPoint);
        Assert.Equal(2, session.Rolls.Count);
        var roll = session.Rolls[0];
        Assert.Equal(("João", "azul", 5, "Bom rola"), (roll.PartnerName, roll.PartnerBelt, roll.DurationMinutes, roll.Notes));
        Assert.Equal(
            [(true, "armbar", "mount", "por_cima"), (false, null, null, null), (true, null, null, null)],
            roll.Finishes.Select(f => (f.ByMe, f.TechniqueId, f.Position?.TechniqueId, f.Position?.Side)));
        // The position a submission came from is the same game node as the marked one.
        Assert.Equal(roll.Steps[1].NodeId, roll.Finishes[0].NodeId);
        Assert.Equal([("closed-guard", "por_baixo", "bem"), ("mount", "por_cima", "neutro")], roll.Steps.Select(st => (st.TechniqueId, st.Side, st.Outcome)));
        Assert.Equal((0, null, null), (session.Rolls[1].Finishes.Count, session.Rolls[1].PartnerName, session.Rolls[1].DurationMinutes));
    }

    [Fact]
    public async Task Sending_the_same_session_again_does_not_duplicate_it()
    {
        var input = TrainingData.Session(Today);

        Assert.Equal(SaveStatus.Created, (await With(s => s.CreateAsync(_ana, input))).Status);
        var again = await With(s => s.CreateAsync(_ana, input with { DurationMinutes = 30 }));

        Assert.Equal(SaveStatus.AlreadyExists, again.Status);
        Assert.Equal(90, again.Session!.DurationMinutes);
        Assert.Equal(SaveStatus.Conflict, (await With(s => s.CreateAsync(_bia, input))).Status);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.TrainingSessions.CountAsync());
    }

    [Fact]
    public async Task A_user_never_reads_or_changes_someone_elses_session()
    {
        var input = TrainingData.Session(Today) with { FocusPoint = "Base" };
        await With(s => s.CreateAsync(_ana, input));

        Assert.Null(await With(s => s.GetAsync(_bia, input.Id)));
        Assert.Equal(SaveStatus.NotFound, (await With(s => s.UpdateAsync(_bia, input.Id, input with { DurationMinutes = 10 }))).Status);
        Assert.False(await With(s => s.DeleteAsync(_bia, input.Id)));
        Assert.Equal(0, (await With(s => s.ListAsync(_bia, new TrainingFilter()))).Total);
        Assert.Null(await With(s => s.GetOpenFocusAsync(_bia)));
        var focus = (await With(s => s.GetOpenFocusAsync(_ana)))!;
        Assert.Equal(SaveStatus.NotFound, (await With(s => s.ReviewFocusAsync(_bia, focus.Id, new FocusReviewInput("sim")))).Status);

        var stored = (await With(s => s.GetAsync(_ana, input.Id)))!;
        Assert.Equal(90, stored.DurationMinutes);
        Assert.Equal(focus.Id, (await With(s => s.GetOpenFocusAsync(_ana)))!.Id);
    }

    [Fact]
    public async Task A_position_becomes_one_node_per_side_however_often_it_is_marked()
    {
        RollInput Roll(params RollStepInput[] steps) => new("nenhum", Steps: steps);
        await With(s => s.CreateAsync(_ana, TrainingData.Session(Today.AddDays(-7)) with
        {
            Rolls = [Roll(new RollStepInput("closed-guard", "por_baixo"), new RollStepInput("closed-guard", "por_baixo", "mal")), Roll(new RollStepInput("closed-guard", "por_baixo"))],
        }));
        await With(s => s.CreateAsync(_ana, TrainingData.Session(Today) with
        {
            Rolls = [Roll(new RollStepInput("mount", "por_cima", "bem"), new RollStepInput("closed-guard", "por_cima"))],
        }));
        await With(s => s.CreateAsync(_bia, TrainingData.Session(Today) with { Rolls = [Roll(new RollStepInput("closed-guard", "por_baixo"))] }));

        var positions = await With(s => s.GetRecentPositionsAsync(_ana));

        Assert.Equal(3, positions.Recent.Count);
        var guard = positions.Recent.Single(n => n is { TechniqueId: "closed-guard", Side: "por_baixo" });
        Assert.Equal((3, Today.AddDays(-7)), (guard.Uses, guard.LastUsed));
        Assert.Equal(guard, positions.Recent[^1]);
        Assert.Equal(["closed-guard", "mount"], positions.Catalog.Select(c => c.TechniqueId).Order());
        await using var db = database.CreateContext();
        Assert.Equal(4, await db.GameNodes.CountAsync());
    }

    [Fact]
    public async Task A_partner_is_found_again_by_name_and_remembers_the_belt()
    {
        await With(s => s.CreateAsync(_ana, TrainingData.Session(Today.AddDays(-1)) with { Rolls = [new RollInput("João Silva", "branca")] }));
        await With(s => s.CreateAsync(_ana, TrainingData.Session(Today) with
        {
            Rolls = [new RollInput("joao  SILVA", "azul", Finishes: [TrainingData.FuiFinalizado("rear-naked-choke")]), new RollInput("Carla")],
        }));
        await With(s => s.CreateAsync(_bia, TrainingData.Session(Today) with { Rolls = [new RollInput("João Silva")] }));

        var partners = await With(s => s.FindPartnersAsync(_ana, "joão"));

        var joao = Assert.Single(partners);
        Assert.Equal(("joao  SILVA", "azul"), (joao.Name, joao.Belt));
        Assert.Equal(2, (await With(s => s.FindPartnersAsync(_ana, null))).Count);
        Assert.Equal(2, (await With(s => s.ListAsync(_ana, new TrainingFilter(PartnerId: joao.Id)))).Total);
        await using var db = database.CreateContext();
        Assert.Equal(3, await db.Partners.CountAsync());
        // Each roll keeps the belt of its day.
        Assert.Equal([Belt.Branca, Belt.Azul], await db.Rolls.Where(r => r.PartnerId == joao.Id).OrderBy(r => r.Session!.Date)
            .Select(r => r.PartnerBelt).ToListAsync());
    }

    [Fact]
    public async Task Invalid_sessions_are_refused_with_the_field_of_each_problem()
    {
        var input = TrainingData.Session(Today.AddDays(5)) with
        {
            Attire = "kimono", DurationMinutes = 0, Sleep = 6, Techniques = ["nao-existe"],
            Rolls =
            [
                new RollInput(Finishes: [TrainingData.Finalizei("single-leg")], Steps: [new RollStepInput("armbar", "por_cima")]),
                new RollInput(Steps: [new RollStepInput("mount", "de_lado", "otimo")], Finishes: [TrainingData.FuiFinalizado("armbar", new PositionRef("armbar", "por_cima"))]),
                new RollInput(DurationMinutes: 0),
            ],
        };

        var result = await With(s => s.CreateAsync(_ana, input));

        Assert.Equal(SaveStatus.Invalid, result.Status);
        Assert.Equal(
            [
                "attire", "date", "durationMinutes", "rolls[0].finishes[0].techniqueId", "rolls[0].steps[0].techniqueId",
                "rolls[1].finishes[0].position.techniqueId", "rolls[1].steps[0].outcome", "rolls[1].steps[0].side", "rolls[2].durationMinutes", "sleep", "techniques",
            ],
            result.Errors!.Keys.Order());
        await using var db = database.CreateContext();
        Assert.Equal(0, await db.TrainingSessions.CountAsync());
    }

    [Fact]
    public async Task Editing_replaces_the_rolls_tags_and_point_to_fix()
    {
        var input = TrainingData.Session(Today) with
        {
            Techniques = ["armbar"], FocusPoint = "Base",
            Rolls = [new RollInput("João", Finishes: [TrainingData.Finalizei("armbar")]), new RollInput("Carla")],
        };
        await With(s => s.CreateAsync(_ana, input));

        var edited = await With(s => s.UpdateAsync(_ana, input.Id, input with
        {
            Format = "open", Techniques = ["closed-guard"], FocusPoint = "Pegada",
            Rolls = [new RollInput("Carla", Finishes: [TrainingData.FuiFinalizado("rear-naked-choke"), TrainingData.FuiFinalizado()])],
        }));

        Assert.Equal(SaveStatus.Updated, edited.Status);
        var session = edited.Session!;
        Assert.Equal(("open", "Pegada"), (session.Format, session.FocusPoint));
        Assert.Equal(["closed-guard"], session.Techniques);
        Assert.Equal("Carla", Assert.Single(session.Rolls).PartnerName);
        Assert.Equal([(false, "rear-naked-choke"), (false, null)], session.Rolls[0].Finishes.Select(f => (f.ByMe, f.TechniqueId)));

        await With(s => s.UpdateAsync(_ana, input.Id, input with { FocusPoint = " " }));
        Assert.Null(await With(s => s.GetOpenFocusAsync(_ana)));
        await using var db = database.CreateContext();
        Assert.Equal((2, 0, 1), (await db.Rolls.CountAsync(), await db.FocusPoints.CountAsync(), await db.RollFinishes.CountAsync()));
    }

    [Fact]
    public async Task The_latest_open_point_to_fix_is_offered_until_answered()
    {
        var older = TrainingData.Session(Today.AddDays(-3)) with { FocusPoint = "Postura" };
        var newer = TrainingData.Session(Today.AddDays(-1)) with { FocusPoint = "Respiração" };
        var today = TrainingData.Session(Today);
        foreach (var input in new[] { older, newer, today }) await With(s => s.CreateAsync(_ana, input));

        var focus = (await With(s => s.GetOpenFocusAsync(_ana)))!;
        Assert.Equal(("Respiração", newer.Id, newer.Date), (focus.Text, focus.OriginSessionId, focus.OriginDate));

        Assert.Equal(SaveStatus.Invalid, (await With(s => s.ReviewFocusAsync(_ana, focus.Id, new FocusReviewInput("talvez")))).Status);
        Assert.Equal(SaveStatus.Invalid, (await With(s => s.ReviewFocusAsync(_ana, focus.Id, new FocusReviewInput("sim", Guid.NewGuid())))).Status);
        Assert.Equal(SaveStatus.Updated, (await With(s => s.ReviewFocusAsync(_ana, focus.Id, new FocusReviewInput("parcial", today.Id)))).Status);

        Assert.Equal("Postura", (await With(s => s.GetOpenFocusAsync(_ana)))!.Text);
        await using var db = database.CreateContext();
        var answered = await db.FocusPoints.SingleAsync(f => f.Id == focus.Id);
        Assert.Equal((FocusReview.Parcial, today.Id), (answered.Review, answered.ReviewedSessionId));
    }

    [Fact]
    public async Task The_history_is_filtered_and_paged_newest_first()
    {
        var sessions = Enumerable.Range(0, 5).Select(i => TrainingData.Session(Today.AddDays(-i)) with
        {
            Attire = i % 2 == 0 ? "gi" : "no_gi",
            Format = i == 4 ? "competicao" : "aula",
            TechniqueNotes = i == 2 ? "Treinei 100% de raspagem" : null,
            Rolls = i == 3 ? [new RollInput(Notes: "muita RASPAGEM")] : [],
        }).ToList();
        foreach (var input in sessions) await With(s => s.CreateAsync(_ana, input));

        async Task<List<DateOnly>> Dates(TrainingFilter filter) => (await With(s => s.ListAsync(_ana, filter))).Items.Select(i => i.Date).ToList();

        Assert.Equal(sessions.Select(s => s.Date), await Dates(new TrainingFilter()));
        Assert.Equal([Today, Today.AddDays(-2), Today.AddDays(-4)], await Dates(new TrainingFilter(Attire: "gi")));
        Assert.Equal([Today.AddDays(-4)], await Dates(new TrainingFilter(Format: "competicao")));
        Assert.Equal([Today.AddDays(-2), Today.AddDays(-3)], await Dates(new TrainingFilter(Query: "raspagem")));
        Assert.Equal([Today.AddDays(-2)], await Dates(new TrainingFilter(Query: "100%")));
        Assert.Equal([Today.AddDays(-1), Today.AddDays(-2)], await Dates(new TrainingFilter(From: Today.AddDays(-2), To: Today.AddDays(-1))));

        var page = await With(s => s.ListAsync(_ana, new TrainingFilter(Page: 2, PageSize: 2)));
        Assert.Equal((5, 2, 2), (page.Total, page.Page, page.PageSize));
        Assert.Equal([Today.AddDays(-2), Today.AddDays(-3)], page.Items.Select(i => i.Date));
    }

    [Fact]
    public async Task Deleting_a_session_takes_its_rolls_and_point_to_fix()
    {
        var input = TrainingData.Session(Today) with { FocusPoint = "Base", Rolls = [new RollInput("João", Steps: [new RollStepInput("mount", "por_cima")], Finishes: [TrainingData.Finalizei("armbar")])] };
        await With(s => s.CreateAsync(_ana, input));

        Assert.True(await With(s => s.DeleteAsync(_ana, input.Id)));

        await using var db = database.CreateContext();
        Assert.Equal((0, 0, 0, 0, 0), (await db.TrainingSessions.CountAsync(), await db.Rolls.CountAsync(), await db.RollSteps.CountAsync(), await db.RollFinishes.CountAsync(), await db.FocusPoints.CountAsync()));
        // The partner and the position stay: they belong to the user's game, not to the session.
        Assert.Equal((1, 1), (await db.Partners.CountAsync(), await db.GameNodes.CountAsync()));
    }

    [Fact]
    public async Task The_export_has_everything_and_deleting_the_account_removes_it()
    {
        var input = TrainingData.Session(Today) with
        {
            FocusPoint = "Base",
            Rolls = [new RollInput("João", "roxa", Steps: [new RollStepInput("mount", "por_cima", "bem")], Finishes: [TrainingData.Finalizei("armbar")])],
        };
        await With(s => s.CreateAsync(_ana, input));
        await With(s => s.CreateAsync(_bia, TrainingData.Session(Today) with { Rolls = [new RollInput("Carla")] }));

        var export = (await With(s => s.ExportAsync(_ana)))!;
        Assert.Equal((_ana, "ana@exemplo.com", "Ana"), (export.UserId, export.Email, export.Name));
        Assert.Equal(input.Id, Assert.Single(export.Sessions).Id);
        Assert.Equal("armbar", Assert.Single(export.Sessions[0].Rolls[0].Finishes).TechniqueId);
        Assert.Equal(("João", "roxa"), (Assert.Single(export.Partners).Name, export.Partners[0].Belt));
        Assert.Equal("mount", Assert.Single(export.GameNodes).TechniqueId);
        Assert.Equal(("Base", null), (Assert.Single(export.FocusPoints).Text, export.FocusPoints[0].Review));

        Assert.True(await new DbAccountStore(database.CreateContext()).DeleteAsync(_ana));

        Assert.Null(await With(s => s.ExportAsync(_ana)));
        await using var db = database.CreateContext();
        Assert.Equal(_bia, (await db.TrainingSessions.SingleAsync()).UserId);
        Assert.Equal(_bia, (await db.Partners.SingleAsync()).UserId);
        Assert.Equal((0, 0), (await db.GameNodes.CountAsync(), await db.FocusPoints.CountAsync()));
        Assert.Equal("bia", (await db.UserLogins.SingleAsync()).ProviderKey);
    }
}

/// <summary>Catalog and sessions shared by the diary tests.</summary>
internal static class TrainingData
{
    public static async Task SeedTechniquesAsync(PostgresFixture database)
    {
        await using var db = database.CreateContext();
        db.Techniques.AddRange(
            new TechniqueEntity { Id = "mount", English = "Mount", Portuguese = "Montada", Category = "Posições", Position = 1 },
            new TechniqueEntity { Id = "closed-guard", English = "Closed Guard", Portuguese = "Guarda fechada", Category = "Guardas", Position = 2 },
            new TechniqueEntity { Id = "armbar", English = "Armbar", Portuguese = "Chave de braço", Category = "Chaves de braço", Position = 3 },
            new TechniqueEntity { Id = "rear-naked-choke", English = "Rear Naked Choke", Portuguese = "Mata-leão", Category = "Estrangulamentos", Position = 4 },
            new TechniqueEntity { Id = "single-leg", English = "Single Leg", Portuguese = "Single leg", Category = "Quedas", Position = 5 });
        await db.SaveChangesAsync();
    }

    public static TrainingSessionInput Session(DateOnly date) => new(Guid.NewGuid(), date, "no_gi", "aula", 90);

    public static RollFinishInput Finalizei(string? technique = null, PositionRef? position = null) => new(true, technique, position);

    public static RollFinishInput FuiFinalizado(string? technique = null, PositionRef? position = null) => new(false, technique, position);
}
