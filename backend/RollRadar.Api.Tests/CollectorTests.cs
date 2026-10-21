using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Data;
using RollRadar.Api.Techniques;
using RollRadar.Coletor;

namespace RollRadar.Api.Tests;

public class CaptionParsingTests
{
    private static readonly string[] English = ["en"];

    private static YtDlpOutput Output(JsonObject? info, int exitCode = 0, string[]? errors = null, string[]? warnings = null) =>
        new(exitCode, info is null ? [] : [info.ToJsonString()], errors ?? [], warnings ?? []);

    private static JsonObject Info(JsonObject? subtitles = null, JsonObject? automatic = null) => new()
    {
        ["id"] = "abc", ["upload_date"] = "20240602", ["duration"] = 600.4, ["view_count"] = 1234,
        ["subtitles"] = subtitles ?? new JsonObject(), ["automatic_captions"] = automatic ?? new JsonObject(),
    };

    private static JsonArray Json3() => [new JsonObject { ["ext"] = "vtt", ["url"] = "a" }, new JsonObject { ["ext"] = "json3", ["url"] = "b" }];

    private const string Caption = """
        {"events": [
          {"tStartMs": 10640, "dDurationMs": 2000, "segs": [{"utf8": "he goes "}, {"utf8": "for the  berimbolo"}]},
          {"tStartMs": 12000, "segs": [{"utf8": "\n"}]},
          {"tStartMs": 13050, "segs": [{"utf8": "and takes the back"}]}
        ]}
        """;

    [Fact]
    public void Reads_json3_events_as_lines()
    {
        var segments = CaptionParsing.ParseJson3(Caption);

        Assert.Equal([(10.6, "he goes for the berimbolo"), (13.1, "and takes the back")], segments.Select(s => (s.Start, s.Text)));
        Assert.Equal(2, segments[0].Duration);
    }

    [Fact]
    public void Prefers_the_channel_track_then_the_automatic_original_and_never_translations()
    {
        Assert.Equal(("en", "en"), CaptionParsing.ChooseTrack(Info(new JsonObject { ["en"] = Json3() }, new JsonObject { ["en-orig"] = Json3() }), English));
        Assert.Equal(("en", "en-orig"), CaptionParsing.ChooseTrack(Info(automatic: new JsonObject { ["en-orig"] = Json3(), ["en"] = Json3() }), English));
        Assert.Null(CaptionParsing.ChooseTrack(Info(automatic: new JsonObject { ["en"] = Json3(), ["pt-orig"] = Json3() }), English));
    }

    [Fact]
    public void Reads_the_caption_file_and_the_video_details()
    {
        var answer = CaptionParsing.Interpret(Output(Info(automatic: new JsonObject { ["en-orig"] = Json3() })),
            new Dictionary<string, string> { ["abc.en-orig.json3"] = Caption }, English);

        var found = Assert.IsType<CaptionsFound>(answer);
        Assert.Equal("en", found.Language);
        Assert.Equal(2, found.Segments.Count);
        Assert.Equal(new VideoDetails(new DateOnly(2024, 6, 2), 600, 1234), found.Details);
    }

    [Fact]
    public void Tells_disabled_captions_from_missing_languages()
    {
        var none = new Dictionary<string, string>();
        Assert.Equal(new NoCaptions("TranscriptsDisabled"), CaptionParsing.Interpret(Output(Info()), none, English));
        Assert.Equal(new NoCaptions("NoTranscriptFound"),
            CaptionParsing.Interpret(Output(Info(automatic: new JsonObject { ["pt-orig"] = Json3() })), none, English));
    }

    [Theory]
    [InlineData("[youtube] abc: Sign in to confirm you're not a bot", "IpBlocked")]
    [InlineData("Unable to download webpage: HTTP Error 429: Too Many Requests", "TooManyRequests")]
    public void Blocks_raise_so_the_same_fight_is_tried_again(string error, string name)
    {
        var e = Assert.Throws<BlockedException>(() =>
            CaptionParsing.Interpret(Output(null, 1, [error]), new Dictionary<string, string>(), English));
        Assert.Equal(name, e.Message);
    }

    [Fact]
    public void A_429_on_the_caption_download_is_a_block_too()
    {
        var output = Output(Info(automatic: new JsonObject { ["en-orig"] = Json3() }),
            warnings: ["[youtube] abc: Unable to download video subtitles for 'en-orig': HTTP Error 429: Too Many Requests"]);

        Assert.Throws<BlockedException>(() => CaptionParsing.Interpret(output, new Dictionary<string, string>(), English));
    }

    [Theory]
    [InlineData("[youtube] abc: Private video. Sign in if you've been granted access", "VideoUnavailable")]
    [InlineData("[youtube] abc: Sign in to confirm your age. This video may be inappropriate for some users.", "AgeRestricted")]
    [InlineData("[youtube] abc: Join this channel to get access to members-only content", "VideoUnplayable")]
    public void Unavailable_videos_have_no_captions(string error, string name)
    {
        Assert.Equal(new NoCaptions(name), CaptionParsing.Interpret(Output(null, 1, [error]), new Dictionary<string, string>(), English));
    }

    [Fact]
    public void Unknown_errors_are_temporary()
    {
        Assert.Throws<TemporaryFailureException>(() =>
            CaptionParsing.Interpret(Output(null, 1, ["something new broke"]), new Dictionary<string, string>(), English));
    }

    [Fact]
    public void A_missing_po_token_is_counted_apart()
    {
        var withWarning = Output(Info(), warnings: ["[youtube] abc: There are missing subtitles languages because a PO token was not provided."]);
        Assert.Throws<PoTokenMissingException>(() => CaptionParsing.Interpret(withWarning, new Dictionary<string, string>(), English));

        var empty = Output(Info(automatic: new JsonObject { ["en-orig"] = Json3() }));
        Assert.Throws<PoTokenMissingException>(() =>
            CaptionParsing.Interpret(empty, new Dictionary<string, string> { ["abc.en-orig.json3"] = " " }, English));
    }
}

public class PacingTests
{
    private readonly List<TimeSpan> _sleeps = [];

    private Pacing Create(int batch = 3, int maxBlocks = 2, int? maxRequests = null) =>
        new(10, batch, 5, 60, maxBlocks, maxRequests, (t, _) =>
        {
            _sleeps.Add(t);
            return Task.CompletedTask;
        }, new Random(1));

    private sealed class Source(params Func<CaptionAnswer>[] answers) : ICaptionSource
    {
        private int _next;

        public Task<CaptionAnswer> FetchAsync(string videoId, CancellationToken cancellation) => Task.FromResult(answers[_next++]());
    }

    [Fact]
    public async Task Pauses_irregularly_and_rests_every_batch()
    {
        var pacing = Create();
        for (var i = 0; i < 3; i++) await pacing.RequestedAsync(CancellationToken.None);

        Assert.All(_sleeps.Take(2), s => Assert.InRange(s.TotalSeconds, 5, 15));
        Assert.Equal(TimeSpan.FromMinutes(5), _sleeps[2]);
    }

    [Fact]
    public async Task Does_not_wait_after_the_last_request_of_the_session()
    {
        var pacing = Create(maxRequests: 1);
        await pacing.RequestedAsync(CancellationToken.None);

        Assert.False(pacing.CanRequest);
        Assert.Empty(_sleeps);
    }

    [Fact]
    public async Task Waits_longer_after_each_block_and_tries_the_same_fight_again()
    {
        var pacing = Create();
        var answer = await pacing.FetchAsync(new Source(
            () => throw new BlockedException("IpBlocked"),
            () => throw new BlockedException("IpBlocked"),
            () => new NoCaptions("TranscriptsDisabled")), "abc", CancellationToken.None);

        Assert.Equal(new NoCaptions("TranscriptsDisabled"), answer);
        Assert.Equal([TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(120)], _sleeps.Take(2));
        Assert.Equal(1, pacing.Requests);
        Assert.Equal(0, pacing.BlocksInARow);
    }

    [Fact]
    public async Task Gives_up_after_too_many_blocks_in_a_row()
    {
        var pacing = Create(maxBlocks: 1);
        var source = new Source(() => throw new BlockedException("IpBlocked"), () => throw new BlockedException("IpBlocked"));

        await Assert.ThrowsAsync<GiveUpException>(() => pacing.FetchAsync(source, "abc", CancellationToken.None));
    }

    [Fact]
    public async Task A_temporary_failure_counts_as_a_request_and_leaves_the_fight_pending()
    {
        var pacing = Create();
        var answer = await pacing.FetchAsync(new Source(() => throw new TemporaryFailureException("x")), "abc", CancellationToken.None);

        Assert.Null(answer);
        Assert.Equal(1, pacing.Requests);
    }
}

public class FightQueueTests
{
    private static VideoEntity Video(string id, CaptionStatus status, int? duration = 600, long? views = 0, int channel = 1,
        string? @event = null, string? error = null) =>
        new()
        {
            Id = id, Title = id, CaptionStatus = status, DurationSeconds = duration, ViewCount = views, ChannelId = channel,
            Event = @event, CaptionError = error,
        };

    [Theory]
    [InlineData("Meregali vs Buchecha", false, true)]
    [InlineData("Meregali VS. Buchecha", false, true)]
    [InlineData("Meregali versus Buchecha", false, true)]
    [InlineData("Meregali x Buchecha", false, false)]
    [InlineData("Meregali x Buchecha", true, true)]
    [InlineData("Best of vs", false, false)]
    public void Recognizes_fight_titles(string title, bool includeX, bool expected) =>
        Assert.Equal(expected, FightQueue.IsFight(title, includeX));

    [Theory]
    [InlineData("A vs B / Manaus Open 2026", "Manaus Open 2026")]
    [InlineData("A vs B | FULL MATCH | ADCC 2026", "ADCC 2026")]
    [InlineData("A vs B", null)]
    public void Takes_the_event_from_the_end_of_the_title(string title, string? expected) =>
        Assert.Equal(expected, FightQueue.EventOf(title));

    [Fact]
    public void Reevaluates_short_fights_skipped_events_and_old_fights_without_captions()
    {
        var videos = new[]
        {
            Video("short", CaptionStatus.Pendente, duration: 60),
            Video("long-again", CaptionStatus.Curta, duration: 600),
            Video("no-duration", CaptionStatus.Pendente, duration: null),
            Video("skipped", CaptionStatus.PuladaEvento),
            Video("old-none", CaptionStatus.SemLegenda),
            Video("known-none", CaptionStatus.SemLegenda, error: "TranscriptsDisabled"),
        };

        FightQueue.Reevaluate(videos, 180, noEventSkip: true, redoWithoutCaptions: true);

        Assert.Equal(
            [CaptionStatus.Curta, CaptionStatus.Pendente, CaptionStatus.Pendente, CaptionStatus.Pendente, CaptionStatus.Pendente, CaptionStatus.SemLegenda],
            videos.Select(v => v.CaptionStatus));
    }

    [Fact]
    public void Orders_pending_fights_by_channel_then_views_then_duration()
    {
        var videos = new[]
        {
            Video("second-channel", CaptionStatus.Pendente, views: 999, channel: 2),
            Video("few-views", CaptionStatus.Pendente, views: 1),
            Video("many-views-short", CaptionStatus.Pendente, views: 50, duration: 300),
            Video("many-views-long", CaptionStatus.Pendente, views: 50, duration: 900),
            Video("done", CaptionStatus.Legenda, views: 5000),
        };

        var order = FightQueue.InPriorityOrder(videos, v => v.ChannelId);

        Assert.Equal(["many-views-long", "many-views-short", "few-views", "second-channel"], order.Select(v => v.Id));
    }

    [Fact]
    public void Skips_an_event_after_enough_fights_without_captions_and_none_with()
    {
        var tally = new EventTally([Video("a", CaptionStatus.SemLegenda, @event: "Open"), Video("b", CaptionStatus.SemLegenda, @event: "Open")], 2);
        Assert.True(tally.Exhausted("Open"));
        Assert.False(tally.Exhausted(null));

        tally.Count("Open", CaptionStatus.Legenda);
        Assert.False(tally.Exhausted("Open"));
        Assert.False(new EventTally([Video("a", CaptionStatus.SemLegenda, @event: "Open")], 0).Exhausted("Open"));
    }

    [Fact]
    public void Arguments_take_lists_and_flags()
    {
        var arguments = new Arguments(["coletar", "--canais", "a", "b", "--sem-listar", "--pausa=2,5", "--max-consultas", "30"]);

        Assert.Equal("coletar", arguments.Command);
        Assert.Equal(["a", "b"], arguments.Values("canais"));
        Assert.True(arguments.Flag("sem-listar"));
        Assert.Equal(2.5, arguments.Number("pausa", 20));
        Assert.Equal(30, arguments.OptionalInteger("max-consultas"));
        Assert.Equal(20, arguments.Integer("lote", 20));
        Assert.Empty(arguments.Unused);
    }
}

/// <summary>The coletar command against a real database, with captions from a fake source instead of YouTube.</summary>
[Collection(PostgresCollection.Name)]
public sealed class CollectorTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly Technique[] Techniques =
    [
        new("Berimbolo", "Berimbolo", "Raspagens", ["Berimbolo"], []),
        new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock"]),
    ];

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class FakeSource(Dictionary<string, CaptionAnswer> answers) : ICaptionSource
    {
        public List<string> Asked { get; } = [];

        public Task<CaptionAnswer> FetchAsync(string videoId, CancellationToken cancellation)
        {
            Asked.Add(videoId);
            return Task.FromResult(answers[videoId]);
        }
    }

    private static CaptionsFound Found(params (double Start, string Text)[] lines) =>
        new("en", lines.Select(l => new CaptionSegment(l.Start, 2, l.Text)).ToList(), new VideoDetails(new DateOnly(2026, 1, 2), 700, 99));

    private async Task SeedAsync()
    {
        await using var db = database.CreateContext();
        var channel = new ChannelEntity { Name = "FloGrappling", YoutubeChannelId = "UC1", Url = "https://www.youtube.com/@FloGrappling" };
        db.Channels.Add(channel);
        db.Techniques.AddRange(Techniques.Select((t, i) => new TechniqueEntity
        {
            Id = t.Id, English = t.English, Portuguese = t.Portuguese, Category = t.Category, Position = i,
        }));
        await db.SaveChangesAsync();
        db.Videos.AddRange(
            new VideoEntity { Id = "with", ChannelId = channel.Id, Title = "A vs B / Open", ViewCount = 30, Event = "Open" },
            new VideoEntity { Id = "without", ChannelId = channel.Id, Title = "C vs D / Open", ViewCount = 20, Event = "Open" },
            new VideoEntity { Id = "short", ChannelId = channel.Id, Title = "E vs F", ViewCount = 10, DurationSeconds = 30 });
        await db.SaveChangesAsync();
    }

    private Collector Create(ICaptionSource source, int eventSkip = 5) => new(
        database.CreateContext, new YtDlp("unused", false), source, new TechniqueMatcher(Techniques),
        new Pacing(0, 0, 0, 0, 1, null, (_, _) => Task.CompletedTask),
        new CollectorOptions(["https://www.youtube.com/@FloGrappling"], false, NoListing: true, false, 180, eventSkip, false));

    [Fact]
    public async Task Saves_captions_and_moments_of_each_fight_and_skips_short_ones()
    {
        await SeedAsync();
        var source = new FakeSource(new()
        {
            ["with"] = Found((10, "a berimbolo"), (50, "then an armlock")),
            ["without"] = new NoCaptions("TranscriptsDisabled"),
        });

        var collector = Create(source);
        await collector.RunAsync(CancellationToken.None);

        Assert.Equal(["with", "without"], source.Asked);
        await using var db = database.CreateContext();
        var videos = await db.Videos.OrderBy(v => v.Id).ToDictionaryAsync(v => v.Id);
        Assert.Equal(CaptionStatus.Legenda, videos["with"].CaptionStatus);
        Assert.Equal((DateOnly?)new DateOnly(2026, 1, 2), videos["with"].UploadDate);
        Assert.Equal("en", videos["with"].Language);
        Assert.NotNull(videos["with"].CaptionCheckedAt);
        Assert.Equal((CaptionStatus.SemLegenda, "TranscriptsDisabled"), (videos["without"].CaptionStatus, videos["without"].CaptionError));
        Assert.Equal(CaptionStatus.Curta, videos["short"].CaptionStatus);
        Assert.Equal(2, await db.TranscriptSegments.CountAsync(s => s.VideoId == "with"));
        Assert.Equal(
            [("berimbolo", 10, "berimbolo"), ("armbar", 50, "armlock")],
            (await db.TechniqueMoments.OrderBy(m => m.StartSeconds).ToListAsync()).Select(m => (m.TechniqueId, m.StartSeconds, m.Term)));
        Assert.Equal((1, 1), (collector.WithCaptions, collector.WithoutCaptions));
    }

    [Fact]
    public async Task Skips_the_rest_of_an_event_without_captions()
    {
        await SeedAsync();
        var source = new FakeSource(new() { ["with"] = new NoCaptions("TranscriptsDisabled") });

        var collector = Create(source, eventSkip: 1);
        await collector.RunAsync(CancellationToken.None);

        Assert.Equal(["with"], source.Asked);
        await using var db = database.CreateContext();
        Assert.Equal(CaptionStatus.PuladaEvento, (await db.Videos.SingleAsync(v => v.Id == "without")).CaptionStatus);
        Assert.Equal(1, collector.Skipped);
    }

    [Fact]
    public async Task Reprocessing_keeps_curated_moments_and_drops_the_rest_that_no_longer_match()
    {
        await SeedAsync();
        await Create(new FakeSource(new()
        {
            ["with"] = Found((10, "a berimbolo"), (50, "then an armlock")),
            ["without"] = Found((5, "an armbar")),
        })).RunAsync(CancellationToken.None);
        await using (var db = database.CreateContext())
        {
            var curated = await db.TechniqueMoments.SingleAsync(m => m.VideoId == "with" && m.TechniqueId == "armbar");
            curated.CurationStatus = CurationStatus.Bom;
            await db.SaveChangesAsync();
        }

        // Armbar left the catalog's search: its moments go, except the curated one.
        var stats = new ImportStats();
        await new MomentRebuilder(database.CreateContext, stats).RunAsync(new TechniqueMatcher(Techniques.Take(1)));

        await using var check = database.CreateContext();
        Assert.Equal(
            [("with", "armbar"), ("with", "berimbolo")],
            (await check.TechniqueMoments.OrderBy(m => m.VideoId).ThenBy(m => m.TechniqueId).ToListAsync()).Select(m => (m.VideoId, m.TechniqueId)));
        Assert.Equal(1, stats["technique_moments"].Deleted);
    }
}
