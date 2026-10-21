using RollRadar.Api.Data;
using RollRadar.Api.Techniques;
using RollRadar.Coletor;

namespace RollRadar.Api.Tests;

public class ImporterSourcesTests
{
    private const string QueueJson = """
        {"lutas": {
          "ONRvSkcWfHo": {"channelid": "UCeOvhU6RbmEj8GKFiIMWxKQ", "channelname": "FloGrappling",
            "canal": "https://www.youtube.com/@FloGrappling", "id": "ONRvSkcWfHo",
            "title": "Chris Wojcik vs Santeri Lillius | FULL MATCH", "duracao": 1158, "views": 24000,
            "evento": "2026 ADCC World Championships", "status": "sem_legenda"},
          "pendingOnly": {"channelid": "UCeOvhU6RbmEj8GKFiIMWxKQ", "channelname": "FloGrappling", "id": "pendingOnly",
            "title": "A vs B", "duracao": 600.4, "views": 10, "status": "pendente"}
        }, "atualizado_em": "2026-10-18T10:00:00+00:00"}
        """;

    private const string RegistryJson = """
        {"lutas": {
          "ONRvSkcWfHo": {"channelid": "UCeOvhU6RbmEj8GKFiIMWxKQ", "channelname": "FloGrappling", "id": "ONRvSkcWfHo",
            "title": "Chris Wojcik vs Santeri Lillius | FULL MATCH", "uploaddate": null, "lang": null},
          "registryOnly": {"channelid": "UCuEclQFX1C12JpywNDNMHIg", "channelname": "IBJJF", "id": "registryOnly",
            "title": "C vs D", "uploaddate": "2026-05-01", "lang": "en"}
        }, "canais": [], "idiomas": ["en"], "filtro_titulo": "vs", "parcial": true}
        """;

    [Theory]
    [InlineData("pendente", CaptionStatus.Pendente)]
    [InlineData("legenda", CaptionStatus.Legenda)]
    [InlineData("sem_legenda", CaptionStatus.SemLegenda)]
    [InlineData("curta", CaptionStatus.Curta)]
    [InlineData("pulada_evento", CaptionStatus.PuladaEvento)]
    public void ParseCaptionStatus_MapsQueueStatuses(string status, CaptionStatus expected) =>
        Assert.Equal(expected, Sources.ParseCaptionStatus(status));

    [Fact]
    public void ParseCaptionStatus_UnknownIsNull() => Assert.Null(Sources.ParseCaptionStatus("outro"));

    [Fact]
    public void MergeVideos_CombinesQueueRegistryAndDates()
    {
        var dates = Sources.ReadDates("""{"ONRvSkcWfHo": "2026-10-14", "bad": "ontem"}""");

        var videos = Sources.MergeVideos(Sources.ReadFights(QueueJson), Sources.ReadFights(RegistryJson), dates)
            .ToDictionary(v => v.Id);

        Assert.Equal(3, videos.Count);
        var fight = videos["ONRvSkcWfHo"];
        Assert.Equal(CaptionStatus.SemLegenda, fight.CaptionStatus);
        Assert.Equal(1158, fight.DurationSeconds);
        Assert.Equal(24000, fight.ViewCount);
        Assert.Equal(new DateOnly(2026, 10, 14), fight.UploadDate);
        Assert.Equal("UCeOvhU6RbmEj8GKFiIMWxKQ", fight.ChannelId);

        Assert.Equal(600, videos["pendingOnly"].DurationSeconds);
        Assert.Null(videos["pendingOnly"].UploadDate);

        var registryOnly = videos["registryOnly"];
        Assert.Equal(CaptionStatus.Legenda, registryOnly.CaptionStatus);
        Assert.Equal("en", registryOnly.Language);
        Assert.Equal(new DateOnly(2026, 5, 1), registryOnly.UploadDate);
    }

    [Fact]
    public void ReadTranscript_ReadsSegments()
    {
        var transcript = Sources.ReadTranscript("""
            {"lang": "en", "idiomas": ["en"], "fonte": "yt-dlp",
             "trechos": [{"start": 0.0, "text": "Gordon Ryan"}, {"start": 2.4, "text": "any predictions"}]}
            """);

        Assert.Equal("en", transcript.Language);
        Assert.Equal([new SegmentSource(0, "Gordon Ryan"), new SegmentSource(2.4, "any predictions")], transcript.Segments);
    }

    [Fact]
    public void ReadTranscript_WithoutCaptionsIsEmpty()
    {
        var transcript = Sources.ReadTranscript("""{"lang": null, "trechos": [], "erro": "TranscriptsDisabled"}""");
        Assert.Null(transcript.Language);
        Assert.Empty(transcript.Segments);
    }

    [Fact]
    public void ReadTranscript_TruncatedFileThrows() =>
        // A file a script is still writing: the importer catches this and skips the file.
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => Sources.ReadTranscript("""{"lang": "en", "trechos": [{"sta"""));

    [Fact]
    public void SegmentFingerprint_CountsCodePointsLikePostgres()
    {
        var fingerprint = SegmentFingerprint.Of([new(1, "ok 👍"), new(5.5, "ação")]);
        Assert.Equal(new SegmentFingerprint(2, 8, 5.5), fingerprint);
    }

    [Fact]
    public void ReadTechniqueFileVideos_ReadsVideoMetadata()
    {
        var videos = Sources.ReadTechniqueFileVideos("""
            [{"channelid": "UCeOvhU6RbmEj8GKFiIMWxKQ", "channelname": "FloGrappling", "id": "GUKuWtpptRs",
              "title": "Tomy Langaker vs Eoghan O'Flanagan", "uploaddate": "2026-10-09", "lang": "en",
              "hits": [{"start": 255.7, "termo": "armbar", "text": "an armbar"}]}]
            """);

        var video = Assert.Single(videos!);
        Assert.Equal("GUKuWtpptRs", video.Id);
        Assert.Equal("FloGrappling", video.ChannelName);
        Assert.Equal(new DateOnly(2026, 10, 9), video.UploadDate);
        Assert.Equal(CaptionStatus.Legenda, video.CaptionStatus);
    }

    [Theory]
    [InlineData("""{"versao_formato": 2, "tecnicas": []}""")]
    [InlineData("""[{"video_id": "x", "tecnicas": []}]""")]
    public void ReadTechniqueFileVideos_OtherFilesAreNull(string json) =>
        Assert.Null(Sources.ReadTechniqueFileVideos(json));

    [Fact]
    public void AliasesOf_NormalizesAndDedupesWithinEachKind()
    {
        var technique = new Technique("Back Control / Back Mount", "Pegada nas costas", "Posições",
            ["Back Control", "Back Mount"], ["Pegada nas Costas", "back-mount", "pegada nas costas", "  "]);

        var aliases = Sources.AliasesOf(technique);

        Assert.Equal(
            [
                new AliasSource("Back Control", "back control", AliasKind.EnglishTerm, 0),
                new AliasSource("Back Mount", "back mount", AliasKind.EnglishTerm, 1),
                new AliasSource("Pegada nas Costas", "pegada nas costas", AliasKind.Alias, 0),
                // Repeats an English term but is kept: the API lists the aliases as written in the CSV.
                new AliasSource("back-mount", "back mount", AliasKind.Alias, 1),
            ],
            aliases);
    }

    [Fact]
    public void Apply_KeepsStoredValuesWhenSourceHasNone()
    {
        var video = new VideoEntity
        {
            Id = "x", ChannelId = 1, Title = "A vs B", UploadDate = new DateOnly(2026, 1, 2), ViewCount = 5,
            CaptionStatus = CaptionStatus.Pendente,
        };

        var unchanged = DataImporter.Apply(video, new VideoSource("x", null, null, "", null, null, null, null, null), 1);
        var changed = DataImporter.Apply(video, new VideoSource("x", null, null, "A vs B", null, null, 7, "en", CaptionStatus.Legenda), 1);

        Assert.False(unchanged);
        Assert.True(changed);
        Assert.Equal(new DateOnly(2026, 1, 2), video.UploadDate);
        Assert.Equal(7, video.ViewCount);
        Assert.Equal(CaptionStatus.Legenda, video.CaptionStatus);
    }

    [Fact]
    public void SnakeCase_MatchesStoredEnumNames()
    {
        Assert.Equal("pulada_evento", SnakeCase.From(nameof(CaptionStatus.PuladaEvento)));
        Assert.Equal("english_term", SnakeCase.From(nameof(AliasKind.EnglishTerm)));
    }
}
