using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using RollRadar.Api.Accounts;
using RollRadar.Api.Data;
using RollRadar.Api.Training;

namespace RollRadar.Api.Tests;

/// <summary>The diary endpoints: only for signed-in users, each one with their own data.</summary>
[Collection(PostgresCollection.Name)]
public sealed class TrainingApiTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private WebApplicationFactory<Program> _api = null!;
    private Guid _ana;
    private Guid _bia;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await TrainingData.SeedTechniquesAsync(database);
        _ana = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "ana", "ana@exemplo.com", "Ana"));
        _bia = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "bia", "bia@exemplo.com", "Bia"));
        _api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:RollRadar", database.ConnectionString));
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private HttpClient Client(Guid? user = null)
    {
        var client = _api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (user is { } id) client.DefaultRequestHeaders.Add("Cookie", AccountTests.SessionCookie(_api, id));
        return client;
    }

    [Theory]
    [InlineData("GET", "/api/treinos")]
    [InlineData("GET", "/api/treinos/7d6f2a51-8c0e-4a4b-9a43-6a0b1f7f2c11")]
    [InlineData("POST", "/api/treinos")]
    [InlineData("PUT", "/api/treinos/7d6f2a51-8c0e-4a4b-9a43-6a0b1f7f2c11")]
    [InlineData("DELETE", "/api/treinos/7d6f2a51-8c0e-4a4b-9a43-6a0b1f7f2c11")]
    [InlineData("GET", "/api/treinos/foco")]
    [InlineData("PUT", "/api/treinos/foco/1")]
    [InlineData("GET", "/api/parceiros")]
    [InlineData("GET", "/api/mapa/posicoes-recentes")]
    [InlineData("GET", "/api/conta/exportar")]
    [InlineData("DELETE", "/api/conta")]
    public async Task Without_a_session_the_diary_answers_401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task A_session_is_created_once_read_edited_and_deleted_only_by_its_owner()
    {
        var ana = Client(_ana);
        var input = TrainingData.Session(Today) with { Rolls = [new RollInput("João", "azul", Finishes: [TrainingData.Finalizei("armbar")])] };

        var created = await ana.PostAsJsonAsync("/api/treinos", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.EndsWith($"/api/treinos/{input.Id}", created.Headers.Location!.ToString());
        // The client sends it again after a timeout: same session, no copy.
        Assert.Equal(HttpStatusCode.OK, (await ana.PostAsJsonAsync("/api/treinos", input)).StatusCode);
        Assert.Equal(1, (await ana.GetFromJsonAsync<TrainingPage>("/api/treinos"))!.Total);

        var bia = Client(_bia);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.GetAsync($"/api/treinos/{input.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.PutAsJsonAsync($"/api/treinos/{input.Id}", input)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.DeleteAsync($"/api/treinos/{input.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await bia.PostAsJsonAsync("/api/treinos", input)).StatusCode);
        Assert.Equal(0, (await bia.GetFromJsonAsync<TrainingPage>("/api/treinos"))!.Total);
        Assert.Empty((await bia.GetFromJsonAsync<List<Partner>>("/api/parceiros"))!);

        var edited = await ana.PutAsJsonAsync($"/api/treinos/{input.Id}", input with { DurationMinutes = 60 });
        Assert.Equal(60, (await edited.Content.ReadFromJsonAsync<TrainingSession>())!.DurationMinutes);
        Assert.Equal("João", (await ana.GetFromJsonAsync<TrainingSession>($"/api/treinos/{input.Id}"))!.Rolls[0].PartnerName);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/treinos/{input.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ana.GetAsync($"/api/treinos/{input.Id}")).StatusCode);
    }

    [Fact]
    public async Task An_invalid_session_answers_400_with_the_fields()
    {
        var response = await Client(_ana).PostAsJsonAsync("/api/treinos", TrainingData.Session(Today) with
        {
            Energy = 9,
            Rolls = [new RollInput(Finishes: [TrainingData.Finalizei("mount")])],
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!;
        Assert.Equal(["energy", "rolls[0].finishes[0].techniqueId"], problem.Errors.Keys.Order());
    }

    [Fact]
    public async Task Unknown_filters_answer_400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Client(_ana).GetAsync("/api/treinos?formato=seminario")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(_ana).GetAsync("/api/treinos?formato=open&roupa=gi&q=x&pagina=2")).StatusCode);
    }

    [Fact]
    public async Task The_point_to_fix_is_offered_and_answered()
    {
        var ana = Client(_ana);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.GetAsync("/api/treinos/foco")).StatusCode);

        await ana.PostAsJsonAsync("/api/treinos", TrainingData.Session(Today) with { FocusPoint = "Postura" });
        var focus = (await ana.GetFromJsonAsync<FocusPoint>("/api/treinos/foco"))!;
        Assert.Equal("Postura", focus.Text);

        Assert.Equal(HttpStatusCode.NotFound, (await Client(_bia).PutAsJsonAsync($"/api/treinos/foco/{focus.Id}", new FocusReviewInput("sim"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.PutAsJsonAsync($"/api/treinos/foco/{focus.Id}", new FocusReviewInput("sim"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.GetAsync("/api/treinos/foco")).StatusCode);
    }

    [Fact]
    public async Task Recent_positions_come_with_the_catalog()
    {
        var ana = Client(_ana);
        await ana.PostAsJsonAsync("/api/treinos", TrainingData.Session(Today) with
        {
            Rolls = [new RollInput(Steps: [new RollStepInput("closed-guard", "por_baixo", "bem")])],
        });

        var positions = (await ana.GetFromJsonAsync<RecentPositions>("/api/mapa/posicoes-recentes"))!;

        Assert.Equal(("closed-guard", "por_baixo", 1), (Assert.Single(positions.Recent).TechniqueId, positions.Recent[0].Side, positions.Recent[0].Uses));
        Assert.Equal(2, positions.Catalog.Count);
    }

    [Fact]
    public async Task The_account_is_exported_and_deleted_with_all_its_data()
    {
        var ana = Client(_ana);
        await ana.PostAsJsonAsync("/api/treinos", TrainingData.Session(Today) with { Rolls = [new RollInput("João")] });

        var export = await ana.GetAsync("/api/conta/exportar");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.StartsWith("attachment; filename=\"rollradar-dados-", export.Content.Headers.ContentDisposition?.ToString() ?? export.Headers.GetValues("Content-Disposition").Single());
        var data = (await export.Content.ReadFromJsonAsync<AccountExport>())!;
        Assert.Equal(("ana@exemplo.com", 1, 1), (data.Email, data.Sessions.Count, data.Partners.Count));

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync("/api/conta")).StatusCode);

        // The same cookie (another device, say) no longer opens anything.
        Assert.Equal(HttpStatusCode.Unauthorized, (await ana.GetAsync("/api/eu")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await ana.GetAsync("/api/treinos")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client(_bia).GetAsync("/api/eu")).StatusCode);
    }
}
