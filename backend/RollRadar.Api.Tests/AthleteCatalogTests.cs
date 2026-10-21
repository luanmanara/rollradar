using RollRadar.Api.Athletes;

namespace RollRadar.Api.Tests;

public class AthleteCatalogTests
{
    private static readonly AthleteCatalog Catalog = new([
        new Athlete("Mica Galvão", ["Mica Galvao", "Micael Galvão"]),
        new Athlete("Marcus Almeida", ["Buchecha"]),
        new Athlete("Ana", []),
    ]);

    [Theory]
    [InlineData("MICA GALVAO", "Mica Galvão")]
    [InlineData("buchecha", "Marcus Almeida")]
    [InlineData("Mica Galvão", "Mica Galvão")]
    public void Resolves_a_name_or_alias_ignoring_case_and_accents(string query, string expected)
    {
        Assert.Equal(expected, Catalog.Resolve(query)?.Name);
    }

    [Fact]
    public void Resolves_only_whole_names()
    {
        Assert.Null(Catalog.Resolve("Mica"));
    }

    [Theory]
    [InlineData("Full Match: Mica Galvao vs. Tye Ruotolo | WNO", true)]
    [InlineData("Recapping WNO 28: Micael Galvão", true)]
    [InlineData("Mica Galvãozinho vs X", false)]
    public void Finds_the_athlete_as_whole_words_in_a_title(string title, bool expected)
    {
        Assert.Equal(expected, Catalog.InTitle(Catalog.All[0], title));
    }

    [Fact]
    public void A_short_name_does_not_match_inside_other_words()
    {
        Assert.False(Catalog.InTitle(Catalog.All[2], "Banana vs Mariana"));
        Assert.True(Catalog.InTitle(Catalog.All[2], "Ana (Atos) vs Bia"));
    }

    [Fact]
    public void The_csv_round_trips_names_and_aliases()
    {
        using var writer = new StringWriter();
        AthleteCsv.Write([new AthleteRow(Catalog.All[0], 12), new AthleteRow(new Athlete("Tye Ruotolo", []), 3, Suggested: true)], writer);

        var athletes = AthleteCsv.Read(new StringReader(writer.ToString()));

        Assert.StartsWith("nome,apelidos,lutas,novo", writer.ToString());
        Assert.Contains("Tye Ruotolo,,3,sim", writer.ToString());
        Assert.Equal(["Mica Galvão", "Tye Ruotolo"], athletes.Select(a => a.Name));
        Assert.Equal(["Mica Galvao", "Micael Galvão"], athletes[0].Aliases);
    }
}
