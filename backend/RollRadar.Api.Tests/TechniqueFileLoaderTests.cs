using RollRadar.Api.Models;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;
using RollRadar.Api.Transcripts;

namespace RollRadar.Api.Tests;

public sealed class TechniqueFileLoaderTests : IDisposable
{
    private static readonly TechniqueCatalog Catalog = new([
        new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock"]),
        new("Omoplata", "Omoplata", "Chaves de braço", ["Omoplata"], []),
        new("Rear Naked Choke", "Mata-leão", "Estrangulamentos", ["Rear Naked Choke"], ["RNC"]),
    ]);

    private readonly string _folder = Directory.CreateTempSubdirectory("saida-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, string json)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, json);
        return path;
    }

    private const string ArmbarJson = """
        [
          {
            "channelid": "UCeOvhU6RbmEj8GKFiIMWxKQ", "channelname": "FloGrappling", "id": "GUKuWtpptRs",
            "title": "Tomy Langaker vs Eoghan O'Flanagan | FULL MATCH | Polaris 39",
            "uploaddate": "2026-10-09", "lang": "en",
            "hits": [
              {"start": 255.7, "termo": "armbar", "text": "Owen. But there's also an armbar as well that you have"},
              {"start": 300.2, "text": "older file without termo, armlock here"}
            ]
          }
        ]
        """;

    [Fact]
    public void Reads_videos_and_tags_moments_with_the_technique_from_the_file_name()
    {
        var video = Assert.Single(TechniqueFileLoader.Load([Write("armbar.json", ArmbarJson)], Catalog));

        Assert.Equal("GUKuWtpptRs", video.Id);
        Assert.Equal("FloGrappling", video.Channel);
        Assert.Equal(new DateOnly(2026, 10, 9), video.UploadDate);
        Assert.Equal("en", video.Language);
        Assert.Equal("https://www.youtube.com/watch?v=GUKuWtpptRs", video.Url);

        Assert.Collection(video.Moments,
            m => Assert.Equal(new TechniqueMoment("armbar", 255, "Owen. But there's also an armbar as well that you have", "armbar"), m),
            m => Assert.Equal(new TechniqueMoment("Armbar", 300, "older file without termo, armlock here", "armbar"), m));
    }

    [Fact]
    public void Skips_files_that_are_not_technique_files()
    {
        var files = new[]
        {
            Write("indice.json", """{"versao_formato": 2, "tecnicas": []}"""),
            Write("lutas.json", """[{"video_id": "abc", "titulo": "A vs B", "tecnicas": []}]"""),
            Write("quebrado.json", "{ not json"),
            Write("armbar.json", ArmbarJson),
        };

        Assert.Equal("GUKuWtpptRs", Assert.Single(TechniqueFileLoader.Load(files, Catalog)).Id);
    }

    [Fact]
    public void Same_video_in_several_technique_files_becomes_one_video()
    {
        var omoplata = ArmbarJson.Replace("\"armbar\"", "\"omoplata\"").Replace("255.7", "100");

        var videos = TechniqueFileLoader.Load([Write("armbar.json", ArmbarJson), Write("omoplata.json", omoplata)], Catalog);

        var video = Assert.Single(videos);
        Assert.Equal(
            ["armbar@255", "armbar@300", "omoplata@100", "omoplata@300"],
            video.Moments.Select(m => $"{m.TechniqueId}@{m.StartSeconds}").Order());
    }

    [Fact]
    public void Portuguese_search_finds_moments_from_technique_files()
    {
        var service = new VideoSearchService(TechniqueFileLoader.Load([Write("armbar.json", ArmbarJson)], Catalog), Catalog);

        var result = service.Search("chave de braço", null);

        Assert.Equal(2, Assert.Single(result.Videos).Moments.Count);
        Assert.Equal(1, service.GetTechniques().Single(t => t.English == "Armbar").VideoCount);
    }

    [Fact]
    public void A_file_named_after_any_technique_of_the_real_catalog_is_loaded()
    {
        var catalog = new TechniqueCatalog(Samples.ReadCatalog());
        var files = catalog.All.Select((t, i) =>
            Write($"{t.Id}.json", ArmbarJson.Replace("\"armbar\"", $"\"{t.Id}\"").Replace("GUKuWtpptRs", $"video{i}"))).ToList();

        var videos = TechniqueFileLoader.Load(files, catalog);

        Assert.Equal(catalog.All.Count, videos.Count);
        Assert.Equal(catalog.All.Select(t => t.Id).Order(), videos.SelectMany(v => v.Moments).Select(m => m.TechniqueId).Distinct().Order());
    }

    [Theory]
    [InlineData("Rear Naked Choke", "rear-naked-choke")]
    [InlineData("D'Arce Choke", "d-arce-choke")]
    [InlineData("Saddle / Honey Hole / 411", "saddle-honey-hole-411")]
    [InlineData("50/50", "50-50")]
    [InlineData("Z-Guard / Knee Shield", "z-guard-knee-shield")]
    public void Technique_id_matches_the_script_file_names(string english, string id)
    {
        Assert.Equal(id, Technique.ToId(english));
    }
}
