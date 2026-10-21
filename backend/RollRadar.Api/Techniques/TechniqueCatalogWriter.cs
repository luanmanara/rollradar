namespace RollRadar.Api.Techniques;

/// <summary>
/// Writes the catalog in the format <see cref="TechniqueCatalogReader"/> reads, so it can be edited in a spreadsheet
/// and imported back.
/// </summary>
public static class TechniqueCatalogWriter
{
    public static void Write(IEnumerable<Technique> techniques, TextWriter writer)
    {
        writer.WriteLine("ingles,portugues,categoria,tambem_conhecido_como");
        foreach (var t in techniques)
            writer.WriteLine(string.Join(",", Field(t.English), Field(t.Portuguese), Field(t.Category), Field(string.Join(", ", t.Aliases))));
    }

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
}
