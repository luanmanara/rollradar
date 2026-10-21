using System.Text;

namespace RollRadar.Api.Common;

public static class TextNormalizer
{
    // Explicit table instead of string.Normalize(FormD): the API runs with InvariantGlobalization,
    // where Unicode normalization does not strip accents.
    private const string Accented = "áàâãäåéèêëíìîïóòôõöúùûüçñý";
    private const string Plain = "aaaaaaeeeeiiiiooooouuuucny";

    /// <summary>
    /// Lower case, no accents, hyphens as spaces and single spaces,
    /// so "Triângulo", "triangulo" and "Meia-guarda" / "meia guarda" compare equal. Accents written as a separate
    /// combining mark ("a" + U+0303, as in some YouTube titles) are dropped too.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (var original in text.Trim().ToLowerInvariant())
        {
            if (original is >= '\u0300' and <= '\u036f') continue;
            var accentIndex = Accented.IndexOf(original);
            var c = accentIndex >= 0 ? Plain[accentIndex] : original == '-' ? ' ' : original;
            if (c == ' ' && builder.Length > 0 && builder[^1] == ' ') continue;
            builder.Append(c);
        }
        return builder.ToString().Trim();
    }
}
