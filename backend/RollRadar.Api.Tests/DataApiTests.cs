using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Auth;
using RollRadar.Api.Data;
using RollRadar.Api.Models;
using RollRadar.Coletor;

namespace RollRadar.Api.Tests;

/// <summary>The API over a real database: the data endpoints need a bearer token, the site's endpoints stay open.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DataApiTests(PostgresFixture database) : IAsyncLifetime
{
    private const string CuratorKey = "chave-do-curador";
    private WebApplicationFactory<Program> _api = null!;

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        var channel = new ChannelEntity { Name = "FloGrappling" };
        db.Techniques.Add(new TechniqueEntity
        {
            Id = "armbar", English = "Armbar", Portuguese = "Chave de braço", Category = "Chaves de braço",
            Aliases = [new TechniqueAliasEntity { TechniqueId = "armbar", Alias = "Armbar", NormalizedAlias = "armbar", Kind = AliasKind.EnglishTerm }],
        });
        db.Videos.Add(new VideoEntity
        {
            Id = "a1", Title = "A vs B", Channel = channel, CaptionStatus = CaptionStatus.Legenda,
            Segments =
            [
                new TranscriptSegmentEntity { VideoId = "a1", StartSeconds = 66.5, DurationSeconds = 3, Text = "he goes for the armbar" },
                new TranscriptSegmentEntity { VideoId = "a1", StartSeconds = 10, Text = "here we go" },
            ],
            Moments =
            [
                new TechniqueMomentEntity
                {
                    VideoId = "a1", TechniqueId = "armbar", StartSeconds = 66, Term = "armbar", Snippet = "he goes for the armbar",
                    CurationStatus = Data.CurationStatus.Bom,
                    Votes =
                    [
                        new MomentVoteEntity { VoterId = "v1", IpHash = "ip1", Useful = true, VotedAt = DateTime.UtcNow },
                        new MomentVoteEntity { VoterId = "v2", IpHash = "ip2", Useful = false, VotedAt = DateTime.UtcNow },
                    ],
                },
                new TechniqueMomentEntity { VideoId = "a1", TechniqueId = "armbar", StartSeconds = 300, Term = "armbar", Snippet = "armbar again" },
            ],
        });
        db.Videos.Add(new VideoEntity { Id = "b2", Title = "C vs D", Channel = channel });
        await db.SaveChangesAsync();
        _api = Api();
    }

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private WebApplicationFactory<Program> Api(int tokenRequestsPerMinute = 60, int unfilteredSearchMaxVideos = 50) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            // Not Development: the API's user-secrets (the real database, the real curator key) stay out of the tests.
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:RollRadar", database.ConnectionString)
            .UseSetting("Curation:AdminKey", CuratorKey)
            .UseSetting("ApiAccess:TokenRequestsPerMinute", tokenRequestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .UseSetting("ApiAccess:UnfilteredSearchMaxVideos", unfilteredSearchMaxVideos.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private Task<string> NewToken(string name = "parceiro", DateTime? expiresAt = null) =>
        new TokenStore(database.CreateContext).CreateAsync(name, [ApiScopes.Read], expiresAt);

    private HttpClient Client(string? token = null, WebApplicationFactory<Program>? api = null)
    {
        var client = (api ?? _api).CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/v1/dados/techniques")]
    [InlineData("/api/v1/dados/videos")]
    [InlineData("/api/v1/dados/moments")]
    [InlineData("/api/v1/dados/videos/a1/transcript")]
    public async Task Data_endpoints_need_a_token(string url)
    {
        var response = await Client().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Unknown_revoked_and_expired_tokens_are_refused()
    {
        var revoked = await NewToken("revogado");
        await new TokenStore(database.CreateContext).RevokeAsync("revogado");
        var expired = await NewToken("expirado", DateTime.UtcNow.AddMinutes(-1));

        foreach (var token in new[] { "rr_nao-existe", revoked, expired })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Client(token).GetAsync("/api/v1/dados/techniques")).StatusCode);
    }

    [Fact]
    public async Task A_token_without_the_read_scope_is_forbidden()
    {
        const string token = "rr_sem-escopo";
        await using (var db = database.CreateContext())
        {
            db.ApiTokens.Add(new ApiTokenEntity { Name = "sem-escopo", TokenHash = ApiTokens.Hash(token), TokenStart = "rr_sem-e", Scopes = ["outro"] });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Client(token).GetAsync("/api/v1/dados/techniques")).StatusCode);
    }

    [Fact]
    public async Task A_valid_token_reads_the_data_and_its_use_is_recorded()
    {
        var client = Client(await NewToken());

        var techniques = await client.GetFromJsonAsync<List<DataTechnique>>("/api/v1/dados/techniques");
        Assert.Equal("armbar", Assert.Single(techniques!).Id);

        var videos = await client.GetFromJsonAsync<DataPage<DataVideo>>("/api/v1/dados/videos");
        Assert.Equal(["a1", "b2"], videos!.Items.Select(v => v.Id));
        Assert.True(videos.Items[0].HasTranscript);
        Assert.Equal(2, videos.Items[0].MomentCount);

        var transcript = await client.GetFromJsonAsync<DataTranscript>("/api/v1/dados/videos/a1/transcript");
        Assert.Equal(["here we go", "he goes for the armbar"], transcript!.Segments.Select(s => s.Text));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/dados/videos/nada/transcript")).StatusCode);

        var good = await client.GetFromJsonAsync<DataPage<DataMoment>>("/api/v1/dados/moments?technique=chave de braço&status=bom");
        var moment = Assert.Single(good!.Items);
        Assert.Equal("a1.armbar.66", moment.Id);
        Assert.Equal("bom", moment.CurationStatus);
        Assert.Equal((1, 1), (moment.VotesUseful, moment.VotesNotUseful));

        await using var db = database.CreateContext();
        Assert.NotNull((await db.ApiTokens.SingleAsync()).LastUsedAt);
    }

    [Fact]
    public async Task Moments_and_videos_come_in_pages()
    {
        var client = Client(await NewToken());

        var first = await client.GetFromJsonAsync<DataPage<DataMoment>>("/api/v1/dados/moments?limit=1");
        Assert.Equal("a1.armbar.66", Assert.Single(first!.Items).Id);
        var second = await client.GetFromJsonAsync<DataPage<DataMoment>>($"/api/v1/dados/moments?limit=1&after={first.Next}");
        Assert.Equal("a1.armbar.300", Assert.Single(second!.Items).Id);
        var last = await client.GetFromJsonAsync<DataPage<DataMoment>>($"/api/v1/dados/moments?limit=1&after={second.Next}");
        Assert.Empty(last!.Items);
        Assert.Null(last.Next);

        var videos = await client.GetFromJsonAsync<DataPage<DataVideo>>("/api/v1/dados/videos?limit=1");
        var nextVideos = await client.GetFromJsonAsync<DataPage<DataVideo>>($"/api/v1/dados/videos?limit=1&after={videos!.Next}");
        Assert.Equal("b2", Assert.Single(nextVideos!.Items).Id);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/dados/moments?status=talvez")).StatusCode);
    }

    [Fact]
    public async Task Each_token_has_its_own_request_limit()
    {
        await using var api = Api(tokenRequestsPerMinute: 2);
        var client = Client(await NewToken("a"), api);
        var other = Client(await NewToken("b"), api);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dados/techniques")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/dados/techniques")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync("/api/v1/dados/techniques")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/api/v1/dados/techniques")).StatusCode);
    }

    [Fact]
    public async Task The_site_endpoints_stay_open_and_curation_still_needs_the_curator_key()
    {
        var client = Client();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/videos?technique=armbar")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/videos/techniques")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/moments/a1.armbar.66/vote")).StatusCode);

        // A data token is not the curator key.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client(await NewToken()).GetAsync("/api/curation")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Curator-Key", CuratorKey);
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/curation")).StatusCode);
    }
}

[Collection(PostgresCollection.Name)]
public sealed class UnfilteredSearchTests(PostgresFixture database) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        var channel = new ChannelEntity { Name = "FloGrappling" };
        db.Techniques.Add(new TechniqueEntity { Id = "armbar", English = "Armbar", Portuguese = "Chave de braço", Category = "Chaves de braço" });
        foreach (var (id, day) in new[] { ("old", 1), ("new", 20), ("mid", 10) })
            db.Videos.Add(new VideoEntity
            {
                Id = id, Title = id, Channel = channel, UploadDate = new DateOnly(2026, 10, day),
                Moments = [new TechniqueMomentEntity { VideoId = id, TechniqueId = "armbar", StartSeconds = 66, Term = "armbar", Snippet = "armbar" }],
            });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Without_a_technique_the_site_search_returns_only_the_most_recent_videos()
    {
        await using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:RollRadar", database.ConnectionString)
            .UseSetting("ApiAccess:UnfilteredSearchMaxVideos", "2"));
        var client = api.CreateClient();

        var unfiltered = await client.GetFromJsonAsync<SearchResult>("/api/videos");
        Assert.Equal(["new", "mid"], unfiltered!.Videos.Select(v => v.Id));

        var byTechnique = await client.GetFromJsonAsync<SearchResult>("/api/videos?technique=armbar");
        Assert.Equal(3, byTechnique!.Videos.Count);

        // By athlete only: the fights with the athlete in the title, every technique.
        var byAthlete = await client.GetFromJsonAsync<SearchResult>("/api/videos?athlete=mid");
        Assert.Equal(["mid"], byAthlete!.Videos.Select(v => v.Id));
    }
}

public sealed class ApiTokensTests
{
    [Fact]
    public void Tokens_are_random_and_only_their_hash_is_kept()
    {
        var token = ApiTokens.Generate();

        Assert.StartsWith(ApiTokens.Prefix, token);
        Assert.Equal(46, token.Length);
        Assert.NotEqual(token, ApiTokens.Generate());
        Assert.Matches("^[0-9a-f]{64}$", ApiTokens.Hash(token));
        Assert.Equal(ApiTokens.Hash(token), ApiTokens.Hash(token));
        Assert.Equal(token[..8], ApiTokens.Start(token));
    }
}

[Collection(PostgresCollection.Name)]
public sealed class TokenStoreTests(PostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Creates_lists_and_revokes_tokens_by_name()
    {
        var store = new TokenStore(database.CreateContext);

        var token = await store.CreateAsync("parceiro", [ApiScopes.Read], null);

        var saved = Assert.Single(await store.ListAsync());
        Assert.Equal(ApiTokens.Hash(token), saved.TokenHash);
        Assert.DoesNotContain(token, saved.TokenHash + saved.TokenStart + saved.Name);
        Assert.Equal([ApiScopes.Read], saved.Scopes);

        Assert.True(await store.RevokeAsync("parceiro"));
        Assert.NotNull(Assert.Single(await store.ListAsync()).RevokedAt);
        Assert.False(await store.RevokeAsync("parceiro"));
    }

    [Fact]
    public async Task Refuses_a_repeated_name_and_unknown_scopes()
    {
        var store = new TokenStore(database.CreateContext);
        await store.CreateAsync("parceiro", [ApiScopes.Read], null);

        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("parceiro", [ApiScopes.Read], null));
        await Assert.ThrowsAsync<ArgumentException>(() => store.CreateAsync("outro", ["admin"], null));
    }
}
