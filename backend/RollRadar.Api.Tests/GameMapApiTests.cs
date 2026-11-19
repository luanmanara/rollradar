using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RollRadar.Api.Accounts;
using RollRadar.Api.Data;
using RollRadar.Api.GameMap;
using RollRadar.Api.Training;

namespace RollRadar.Api.Tests;

/// <summary>The game map endpoints: only for signed-in users, each one with their own map.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GameMapApiTests(PostgresFixture database) : IAsyncLifetime
{
    private WebApplicationFactory<Program> _api = null!;
    private Guid _ana;
    private Guid _bia;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await TrainingData.SeedTechniquesAsync(database);
        await using (var db = database.CreateContext())
        {
            db.Videos.Add(new VideoEntity
            {
                Id = "v1", Title = "A vs B", Channel = new ChannelEntity { Name = "FloGrappling" }, CaptionStatus = CaptionStatus.Legenda,
                Moments = [new TechniqueMomentEntity { VideoId = "v1", TechniqueId = "closed-guard", StartSeconds = 40, Term = "closed guard", Snippet = "he pulls closed guard" }],
            });
            await db.SaveChangesAsync();
        }
        _ana = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "ana", null, "Ana"));
        _bia = await new DbAccountStore(database.CreateContext()).SignInAsync(new ExternalLogin("Google", "bia", null, "Bia"));
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
    [InlineData("GET", "/api/mapa")]
    [InlineData("GET", "/api/mapa/nos/1")]
    [InlineData("GET", "/api/mapa/arestas/1")]
    [InlineData("POST", "/api/mapa/nos")]
    [InlineData("PUT", "/api/mapa/nos/1")]
    [InlineData("PUT", "/api/mapa/nos/posicoes")]
    [InlineData("DELETE", "/api/mapa/nos/1")]
    [InlineData("POST", "/api/mapa/arestas")]
    [InlineData("PUT", "/api/mapa/arestas/1")]
    [InlineData("DELETE", "/api/mapa/arestas/1")]
    [InlineData("POST", "/api/mapa/videos")]
    [InlineData("DELETE", "/api/mapa/videos/1")]
    public async Task Without_a_session_the_map_answers_401(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(url.EndsWith("posicoes") ? (object)Array.Empty<object>() : new { });

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task The_map_is_built_through_the_api_and_only_its_owner_sees_it()
    {
        var ana = Client(_ana);
        var guard = await (await ana.PostAsJsonAsync("/api/mapa/nos", new NodeInput("closed-guard", "por_baixo"))).Content.ReadFromJsonAsync<MapNode>();
        var mountResponse = await ana.PostAsJsonAsync("/api/mapa/nos", new NodeInput("mount", "por_cima"));
        Assert.Equal(HttpStatusCode.Created, mountResponse.StatusCode);
        var mount = await mountResponse.Content.ReadFromJsonAsync<MapNode>();

        var sweep = await ana.PostAsJsonAsync("/api/mapa/arestas", new EdgeInput(guard!.Id, mount!.Id, "raspagem", "scissor-sweep"));
        Assert.Equal(HttpStatusCode.Created, sweep.StatusCode);
        var edge = (await sweep.Content.ReadFromJsonAsync<MapEdge>())!;
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/mapa/arestas", new EdgeInput(guard.Id, mount.Id, "raspagem", "knee-cut"))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.PutAsJsonAsync("/api/mapa/nos/posicoes", new[] { new NodePosition(guard.Id, 50, 60) })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await ana.PostAsJsonAsync("/api/mapa/videos", new MapVideoInput(null, edge.Id, "v1", 42))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ana.PostAsJsonAsync("/api/mapa/videos", new MapVideoInput(guard.Id, edge.Id, "v1"))).StatusCode);

        var map = (await ana.GetFromJsonAsync<GameMapView>("/api/mapa"))!;
        Assert.Equal(2, map.Nodes.Count);
        Assert.Equal((50.0, 60.0), (map.Nodes.Single(n => n.Id == guard.Id).X, map.Nodes.Single(n => n.Id == guard.Id).Y));

        var detail = (await ana.GetFromJsonAsync<NodeDetail>($"/api/mapa/nos/{guard.Id}"))!;
        Assert.Equal(edge.Id, Assert.Single(detail.Outgoing).Id);
        // Fights of the position's technique, from the site's search.
        Assert.Equal("v1", Assert.Single(detail.Suggested).Id);
        var edgeDetail = (await ana.GetFromJsonAsync<EdgeDetail>($"/api/mapa/arestas/{edge.Id}"))!;
        Assert.Equal(42, Assert.Single(edgeDetail.Videos).StartSeconds);

        var bia = Client(_bia);
        Assert.Empty((await bia.GetFromJsonAsync<GameMapView>("/api/mapa"))!.Nodes);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.GetAsync($"/api/mapa/nos/{guard.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bia.DeleteAsync($"/api/mapa/arestas/{edge.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/mapa/arestas/{edge.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await ana.DeleteAsync($"/api/mapa/nos/{mount.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_position_used_in_a_roll_is_hidden_not_deleted()
    {
        var ana = Client(_ana);
        await ana.PostAsJsonAsync("/api/treinos", TrainingData.Session(DateOnly.FromDateTime(DateTime.UtcNow)) with
        {
            Rolls = [new RollInput(Steps: [new RollStepInput("closed-guard", "por_baixo", "bem")])],
        });
        var guard = Assert.Single((await ana.GetFromJsonAsync<GameMapView>("/api/mapa"))!.Nodes);

        Assert.Equal(HttpStatusCode.Conflict, (await ana.DeleteAsync($"/api/mapa/nos/{guard.Id}")).StatusCode);
        var hidden = await ana.PutAsJsonAsync($"/api/mapa/nos/{guard.Id}", new NodeUpdate(Hidden: true));
        Assert.True((await hidden.Content.ReadFromJsonAsync<MapNode>())!.Hidden);
    }
}
