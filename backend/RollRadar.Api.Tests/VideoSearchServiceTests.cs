using RollRadar.Api.Models;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;
using RollRadar.Api.Transcripts;

namespace RollRadar.Api.Tests;

public class VideoSearchServiceTests
{
    private static FightVideo Video(string id, string title, params TechniqueMoment[] moments) =>
        new(id, title, $"https://www.youtube.com/watch?v={id}", "", "IBJJF", new DateOnly(2019, 1, 1), moments);

    private static readonly TechniqueCatalog Catalog = new([
        new("Omoplata", "Omoplata", "Chaves de braço", ["Omoplata"], []),
        new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock", "juji-gatame"]),
        new("Triangle Choke", "Triângulo", "Estrangulamentos", ["Triangle Choke"], []),
    ]);

    private readonly VideoSearchService _service = new([
        Video("a", "Nicholas Meregali VS Mahamed Aly", new("Omoplata", 66, ""), new("Triangle", 120, ""), new("Armlock", 200, "")),
        Video("b", "Gabi Garcia vs Carina Santi", new("Omoplata", 10, ""), new("Omoplata", 300, "")),
        Video("c", "Some podcast", new TechniqueMoment("Kimura", 50, "")),
    ], Catalog);

    [Fact]
    public void Portuguese_query_searches_the_English_caption_terms()
    {
        var result = _service.Search("chave de braço", null);

        Assert.Equal("Armbar", Assert.Single(result.Techniques).English);
        Assert.Equal(["Armbar"], result.SearchTerms);
        var video = Assert.Single(result.Videos);
        Assert.Equal("Armlock", Assert.Single(video.Moments).Technique);
    }

    [Fact]
    public void English_query_finds_the_same_technique()
    {
        Assert.Equal("a", Assert.Single(_service.Search("ARMBAR", null).Videos).Id);
    }

    [Fact]
    public void Query_is_matched_ignoring_case_and_accents()
    {
        var result = _service.Search("TRIANGULO", null);

        Assert.Equal("Triângulo", Assert.Single(result.Techniques).Portuguese);
        Assert.Equal(120, Assert.Single(Assert.Single(result.Videos).Moments).StartSeconds);
    }

    [Fact]
    public void Query_not_in_the_catalog_is_searched_as_typed()
    {
        var result = _service.Search("kimura", null);

        Assert.Empty(result.Techniques);
        Assert.Equal(["kimura"], result.SearchTerms);
        Assert.Equal("c", Assert.Single(result.Videos).Id);
    }

    [Fact]
    public void Videos_with_more_moments_come_first()
    {
        Assert.Equal(["b", "a"], _service.Search("omoplata", null).Videos.Select(v => v.Id));
    }

    [Fact]
    public void Filters_by_athlete_in_the_title()
    {
        Assert.Equal("a", Assert.Single(_service.Search("omoplata", "meregali").Videos).Id);
    }

    [Fact]
    public void Techniques_count_the_videos_that_mention_them()
    {
        var techniques = _service.GetTechniques().ToDictionary(t => t.English, t => t.VideoCount);

        Assert.Equal(2, techniques["Omoplata"]);
        Assert.Equal(1, techniques["Armbar"]);
        Assert.Equal(1, techniques["Kimura"]);
    }

    [Fact]
    public void Techniques_include_their_aliases()
    {
        Assert.Equal(["Armlock", "juji-gatame"], _service.GetTechniques().Single(t => t.English == "Armbar").Aliases);
    }

    [Fact]
    public void Searches_the_real_catalog_in_Portuguese_and_in_English()
    {
        var catalog = new TechniqueCatalog(Samples.ReadCatalog());
        var service = new VideoSearchService([
            Video("a", "A vs B", new TechniqueMoment("Mount", 10, "he takes the mount")),
            Video("b", "C vs D", new("Mount", 20, "mount again"), new("Rear Naked Choke", 90, "rear naked choke")),
            Video("c", "E vs F", new TechniqueMoment("Omoplata", 30, "omoplata")),
        ], catalog);

        Assert.Equal(["a", "b"], service.Search("montada", null).Videos.Select(v => v.Id).Order());
        Assert.Equal(["a", "b"], service.Search("Mount", null).Videos.Select(v => v.Id).Order());
        Assert.Equal("b", Assert.Single(service.Search("mata-leão", null).Videos).Id);
    }
}
