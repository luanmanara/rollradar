using RollRadar.Api.Common;

namespace RollRadar.Api.Tests;

public class AthleteNamesTests
{
    [Theory]
    [InlineData("Gordon Ryan vs Felipe Pena | ADCC 2024", "Gordon Ryan", "Felipe Pena")]
    [InlineData("Nicholas Meregali VS Mahamed Aly", "Nicholas Meregali", "Mahamed Aly")]
    [InlineData("Kaynan Duarte vs. Tye Ruotolo (Superfight) / WNO 20", "Kaynan Duarte", "Tye Ruotolo")]
    [InlineData("Jean-Jacques Machado versus Rickson Gracie - Final", "Jean-Jacques Machado", "Rickson Gracie")]
    [InlineData("Ana (Atos) vs Bia (GF Team) / Manaus Open 2026", "Ana", "Bia")]
    [InlineData("Full Match: Mica Galvao vs Tye Ruotolo | WNO", "Mica Galvao", "Tye Ruotolo")]
    public void Takes_the_two_athletes_of_a_fight_title(string title, string first, string second)
    {
        Assert.Equal([first, second], AthleteNames.FromTitle(title));
    }

    [Theory]
    [InlineData("Highlights of the week")]
    [InlineData("A vs B vs C")]
    public void Gives_no_names_for_other_titles(string title)
    {
        Assert.Empty(AthleteNames.FromTitle(title));
    }

    [Fact]
    public void Counts_each_athlete_once_ignoring_case_and_accents()
    {
        var athletes = AthleteNames.FromTitles(["Gordon Ryan vs Felipe Pena", "Andre Galvao vs GORDON RYAN", "André Galvão vs Buchecha"]);

        Assert.Equal(
            [new("André Galvão", 2), new("Gordon Ryan", 2), new("Buchecha", 1), new("Felipe Pena", 1)],
            athletes.Select(a => a with { }).ToArray());
    }
}
