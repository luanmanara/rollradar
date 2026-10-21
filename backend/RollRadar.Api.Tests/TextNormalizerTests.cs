using RollRadar.Api.Common;

namespace RollRadar.Api.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Triângulo", "triangulo")]
    [InlineData("CHAVE DE BRAÇO", "chave de braco")]
    [InlineData("Mata-leão", "mata leao")]
    [InlineData("  Meia-guarda   profunda ", "meia guarda profunda")]
    [InlineData("Raspagem pêndulo", "raspagem pendulo")]
    [InlineData("Saída das costas", "saida das costas")]
    public void Removes_case_accents_and_hyphens(string input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
