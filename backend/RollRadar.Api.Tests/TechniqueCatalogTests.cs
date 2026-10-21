using System.Text;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Tests;

public class TechniqueCatalogTests
{
    private static readonly IReadOnlyList<Technique> SampleTechniques = Samples.ReadCatalog();

    private readonly TechniqueCatalog _catalog = new(SampleTechniques);

    private Technique Find(string english) => _catalog.All.Single(t => t.English == english);

    [Fact]
    public void Reads_every_technique_of_the_sample_catalog()
    {
        Assert.Equal(200, _catalog.All.Count);
        Assert.Equal(_catalog.All.Count, _catalog.All.Select(t => t.English).Distinct().Count());
    }

    [Fact]
    public void Splits_English_alternatives_and_aliases()
    {
        var saddle = Find("Saddle / Honey Hole / 411");
        Assert.Equal(["Saddle", "Honey Hole", "411"], saddle.EnglishTerms);
        Assert.Equal(["50/50"], Find("50/50").EnglishTerms);

        var sideControl = Find("Side Control");
        Assert.Equal("Cem quilos", sideControl.Portuguese);
        Assert.Equal(["Controle lateral", "100 kg"], sideControl.Aliases);
    }

    [Theory]
    [InlineData("chave de braço", "Armbar")]
    [InlineData("Chave de Braco", "Armbar")]
    [InlineData("armlock", "Armbar")]
    [InlineData("mata-leão", "Rear Naked Choke")]
    [InlineData("mata leao", "Rear Naked Choke")]
    [InlineData("RNC", "Rear Naked Choke")]
    [InlineData("cem quilos", "Side Control")]
    [InlineData("100 kg", "Side Control")]
    [InlineData("honey hole", "Saddle / Honey Hole / 411")]
    [InlineData("omoplata", "Omoplata")]
    [InlineData("Raspagem de tesoura", "Scissor Sweep")]
    public void Resolves_Portuguese_English_and_alias_queries(string query, string english)
    {
        Assert.Contains(_catalog.Resolve(query), t => t.English == english);
    }

    [Fact]
    public void Unknown_or_partial_queries_resolve_to_nothing()
    {
        Assert.Empty(_catalog.Resolve("chave"));
        Assert.Empty(_catalog.Resolve("xyz"));
        Assert.Empty(_catalog.Resolve(""));
    }

    [Theory]
    [InlineData("Triangle Choke", "triangle", true)]
    [InlineData("Triangle Choke", "Triangle Choke", true)]
    [InlineData("De La Riva Guard", "de la riva", true)]
    [InlineData("Armbar", "armlock", true)]
    [InlineData("Omoplata", "Omoplata", true)]
    [InlineData("Reverse Triangle", "triangle", false)]
    [InlineData("Triangle Choke", "arm triangle", false)]
    public void Matches_caption_terms(string english, string captionTerm, bool expected)
    {
        Assert.Equal(expected, _catalog.Matches(Find(english), captionTerm));
    }

    [Fact]
    public void Written_catalog_reads_back_the_same()
    {
        var techniques = new List<Technique>(SampleTechniques)
        {
            new("Odd, \"Name\"", "Nome", "Outros", ["Odd, \"Name\""], ["um", "dois"]),
        };
        using var writer = new StringWriter();
        TechniqueCatalogWriter.Write(techniques, writer);

        var read = TechniqueCatalogReader.Read(new StringReader(writer.ToString()));

        Assert.Equal(techniques.Select(t => (t.English, t.Portuguese, t.Category, string.Join("|", t.Aliases))),
            read.Select(t => (t.English, t.Portuguese, t.Category, string.Join("|", t.Aliases))));
    }
}
