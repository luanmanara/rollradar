using System.Text.Json;
using RollRadar.Api.Data;
using RollRadar.Api.Models;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Tests;

/// <summary>The database search must answer exactly like the file-based one over the same data.</summary>
[Collection(PostgresCollection.Name)]
public sealed class DbVideoSearchServiceTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly Technique[] Techniques =
    [
        new("Omoplata", "Omoplata", "Chaves de braço", ["Omoplata"], []),
        new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock", "juji-gatame"]),
        new("Triangle Choke", "Triângulo", "Estrangulamentos", ["Triangle Choke"], []),
        new("Z-Guard / Knee Shield", "Meia-guarda Z", "Guardas", ["Z-Guard", "Knee Shield"], ["Z-guard"]),
        new("Knee Shield", "Escudo de joelho", "Guardas", ["Knee Shield"], []),
    ];

    private static FightVideo Video(string id, string title, string channel, DateOnly? date, params TechniqueMoment[] moments) =>
        new(id, title, $"https://www.youtube.com/watch?v={id}", $"https://i.ytimg.com/vi/{id}/mqdefault.jpg", channel, date,
            moments.Select(m => m with { Id = Curation.MomentKey.For(id, m.TechniqueId, m.Technique, m.StartSeconds) }).ToList(),
            "en");

    // As the file loader produces them: moments by second, then by technique file name.
    private static readonly FightVideo[] Videos =
    [
        Video("a1_b-C", "Nicholas Meregali VS Mahamed Aly", "FloGrappling", new DateOnly(2019, 1, 1),
            new("omoplata", 66, "goes for the omoplata", "omoplata"),
            new("triangle", 120, "a tight triangle", "triangle-choke"),
            new("armlock", 200, "armlock attempt", "armbar"),
            new("knee shield", 300, "knee shield up", "knee-shield"),
            new("knee shield", 300, "knee shield up", "z-guard-knee-shield")),
        Video("b2", "Gabi Garcia vs Carina Santi", "IBJJF", new DateOnly(2020, 5, 5),
            new("omoplata", 10, "early omoplata", "omoplata"),
            new("omoplata", 300, "another omoplata", "omoplata")),
        Video("c3", "Gordon Ryan vs André Galvão", "IBJJF", null,
            new TechniqueMoment("armbar", 50, "armbar from mount", "armbar")),
    ];

    private readonly VideoSearchService _files = new(Videos, new TechniqueCatalog(Techniques));

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        var channels = Videos.Select(v => v.Channel).Distinct()
            .ToDictionary(name => name, name => new ChannelEntity { Name = name, YoutubeChannelId = "UC" + name });
        db.Channels.AddRange(channels.Values);
        db.Techniques.AddRange(Techniques.Select((t, i) => new TechniqueEntity
        {
            Id = t.Id, English = t.English, Portuguese = t.Portuguese, Category = t.Category, Position = i,
            Aliases = t.EnglishTerms.Select((a, p) => (a, p, AliasKind.EnglishTerm))
                .Concat(t.Aliases.Select((a, p) => (a, p, AliasKind.Alias)))
                .Select(x => new TechniqueAliasEntity
                {
                    TechniqueId = t.Id, Alias = x.a, NormalizedAlias = Common.TextNormalizer.Normalize(x.a),
                    Kind = x.Item3, Position = x.p,
                })
                .ToList(),
        }));
        db.Videos.AddRange(Videos.Select(v => new VideoEntity
        {
            Id = v.Id, Title = v.Title, Channel = channels[v.Channel], UploadDate = v.UploadDate, Language = v.Language,
            CaptionStatus = CaptionStatus.Legenda,
            Moments = v.Moments.Select(m => new TechniqueMomentEntity
            {
                VideoId = v.Id, TechniqueId = m.TechniqueId!, StartSeconds = m.StartSeconds, Term = m.Technique, Snippet = m.Snippet,
            }).ToList(),
        }));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private DbVideoSearchService CreateService(RollRadarDbContext db) => new(db, DbTechniqueCatalog.Load(db));

    private static string Json(object value) => JsonSerializer.Serialize(value);

    [Theory]
    [InlineData("", null)]
    [InlineData("omoplata", null)]
    [InlineData("chave de braço", null)]
    [InlineData("ARMBAR", null)]
    [InlineData("juji gatame", null)]
    [InlineData("triangulo", null)]
    [InlineData("knee shield", null)]
    [InlineData("Z-guard", null)]
    [InlineData("shield", null)]
    [InlineData("xyz", null)]
    [InlineData(null, "galvao")]
    [InlineData("armbar", "ryan")]
    public void Search_matches_the_file_based_search(string? technique, string? athlete)
    {
        using var db = database.CreateContext();

        Assert.Equal(Json(_files.Search(technique, athlete)), Json(CreateService(db).Search(technique, athlete)));
    }

    [Fact]
    public void Techniques_match_the_file_based_list_in_catalog_order()
    {
        using var db = database.CreateContext();

        var techniques = CreateService(db).GetTechniques();

        Assert.Equal(Json(_files.GetTechniques()), Json(techniques));
        Assert.Equal(["Omoplata", "Armbar", "Triangle Choke", "Z-Guard / Knee Shield", "Knee Shield"], techniques.Select(t => t.English));
        Assert.Equal(["Z-guard"], techniques[3].Aliases);
    }

    [Fact]
    public void Athletes_come_from_the_titles_of_the_fights_with_moments()
    {
        using var db = database.CreateContext();

        var athletes = CreateService(db).GetAthletes();

        Assert.Equal(Json(_files.GetAthletes()), Json(athletes));
        Assert.Equal(
            ["André Galvão", "Carina Santi", "Gabi Garcia", "Gordon Ryan", "Mahamed Aly", "Nicholas Meregali"],
            athletes.Select(a => a.Name));
    }

    [Fact]
    public void Shared_terms_report_one_moment_per_second()
    {
        using var db = database.CreateContext();

        var video = CreateService(db).Search("", null).Videos.First();

        Assert.Equal([66, 120, 200, 300], video.Moments.Select(m => m.StartSeconds));
        Assert.Equal("a1_b-C.knee-shield.300", video.Moments[^1].Id);
    }

    private async Task Curate(string videoId, string techniqueId, int second, CurationStatus status, params (string Ip, bool Useful)[] votes)
    {
        await using var db = database.CreateContext();
        var moment = db.TechniqueMoments.Single(m => m.VideoId == videoId && m.TechniqueId == techniqueId && m.StartSeconds == second);
        moment.CurationStatus = status;
        moment.Votes.AddRange(votes.Select((v, i) => new MomentVoteEntity
        {
            VoterId = "voter" + i, IpHash = v.Ip, Useful = v.Useful, VotedAt = new DateTime(2026, 10, 19, 12, i, 0, DateTimeKind.Utc),
        }));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Moments_carry_the_curator_verdict_and_the_votes()
    {
        await Curate("b2", "omoplata", 10, CurationStatus.Bom, ("ip1", true), ("ip2", true), ("ip3", false));
        using var db = database.CreateContext();

        var moments = CreateService(db).Search("omoplata", null).Videos.Single(v => v.Id == "b2").Moments;

        Assert.Equal(("bom", 2, 1), (moments[0].Status, moments[0].VotesUseful, moments[0].VotesNotUseful));
        Assert.Equal(("pendente", 0, 0), (moments[1].Status, moments[1].VotesUseful, moments[1].VotesNotUseful));
    }

    [Fact]
    public async Task Moments_marked_bad_are_left_out_except_for_the_curation()
    {
        await Curate("c3", "armbar", 50, CurationStatus.Ruim);
        await Curate("a1_b-C", "armbar", 200, CurationStatus.Bom);
        using var db = database.CreateContext();
        var service = CreateService(db);

        Assert.Equal(["a1_b-C"], service.Search("armbar", null).Videos.Select(v => v.Id));
        Assert.Equal(["a1_b-C", "c3"], service.Search("armbar", null, includeRejected: true).Videos.Select(v => v.Id).Order());

        var armbar = service.GetTechniques().Single(t => t.Id == "armbar");
        Assert.Equal((1, 1), (armbar.VideoCount, armbar.ApprovedMoments));
    }

    [Fact]
    public void Several_techniques_list_first_the_fights_with_more_of_them()
    {
        using var db = database.CreateContext();

        IVideoSearchService service = CreateService(db);
        var result = service.SearchMany(["omoplata", "triangulo"], null);

        Assert.Equal("omoplata + triangulo", result.Query);
        Assert.Equal(["Omoplata", "Triangle Choke"], result.Techniques.Select(t => t.English));
        Assert.Equal(["a1_b-C", "b2"], result.Videos.Select(v => v.Id));
        Assert.Equal([66, 120], result.Videos[0].Moments.Select(m => m.StartSeconds));
        Assert.Equal(Json(((IVideoSearchService)_files).SearchMany(["omoplata", "triangulo"], null)), Json(result));
    }

    [Fact]
    public async Task Videos_carry_their_duration()
    {
        await using (var write = database.CreateContext())
        {
            write.Videos.Single(v => v.Id == "a1_b-C").DurationSeconds = 1158;
            await write.SaveChangesAsync();
        }
        using var db = database.CreateContext();

        var videos = CreateService(db).Search("omoplata", null).Videos;

        Assert.Equal([1158, null], videos.OrderBy(v => v.Id).Select(v => v.DurationSeconds));
    }

    [Fact]
    public async Task With_the_athlete_catalog_the_athletes_and_their_search_follow_it()
    {
        await using (var write = database.CreateContext())
        {
            write.Athletes.AddRange(
                new AthleteEntity { Name = "Meregali", NormalizedName = "meregali", Aliases = ["Nicholas Meregali", "Meregalli"] },
                new AthleteEntity { Name = "André Galvão", NormalizedName = "andre galvao", Aliases = [] },
                new AthleteEntity { Name = "Rickson Gracie", NormalizedName = "rickson gracie", Aliases = [] });
            await write.SaveChangesAsync();
        }
        using var db = database.CreateContext();
        var service = new DbVideoSearchService(db, DbTechniqueCatalog.Load(db), DbAthleteCatalog.Load(db));

        var athletes = service.GetAthletes();
        var byAlias = service.Search(null, "meregalli");
        var byPart = service.Search(null, "gabi");

        Assert.Equal(["André Galvão", "Meregali"], athletes.Select(a => a.Name));
        Assert.Equal(["Nicholas Meregali", "Meregalli"], athletes[1].Aliases);
        Assert.Equal(["a1_b-C"], byAlias.Videos.Select(v => v.Id));
        Assert.Equal(["b2"], byPart.Videos.Select(v => v.Id));
    }

    [Theory]
    [InlineData("a1_b-C.armbar.200", true)]
    [InlineData("a1_b-C.armbar.201", false)]
    [InlineData("c3.omoplata.50", false)]
    [InlineData("not-a-key", false)]
    [InlineData("a.b.c", false)]
    public void MomentExists_looks_up_the_moment_key(string momentId, bool exists)
    {
        using var db = database.CreateContext();

        Assert.Equal(exists, CreateService(db).MomentExists(momentId));
        Assert.Equal(exists, _files.MomentExists(momentId));
    }
}
