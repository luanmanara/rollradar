using RollRadar.Api.Common;

namespace RollRadar.Api.Techniques;

/// <summary>
/// Reads the techniques CSV. Columns: ingles, portugues, categoria, tambem_conhecido_como.
/// </summary>
public static class TechniqueCatalogReader
{
    private static readonly string[] RequiredColumns = ["ingles", "portugues", "categoria", "tambem_conhecido_como"];

    public static IReadOnlyList<Technique> Read(TextReader reader)
    {
        var records = CsvParser.Parse(reader.ReadToEnd());
        if (records.Count == 0) return [];

        var header = CsvParser.RequireColumns(records[0], RequiredColumns, "techniques catalog");
        int Col(string name) => header.IndexOf(name);

        return records
            .Skip(1)
            .Where(r => r.Count == header.Count && r[Col("ingles")].Trim().Length > 0)
            .Select(r => new Technique(
                r[Col("ingles")].Trim(),
                r[Col("portugues")].Trim(),
                r[Col("categoria")].Trim(),
                // Alternatives are written with spaced slashes ("Back Control / Back Mount"), so "50/50" stays whole.
                Split(r[Col("ingles")], " / "),
                Split(r[Col("tambem_conhecido_como")], ",", ";")))
            .ToList();
    }

    private static List<string> Split(string value, params string[] separators) =>
        value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
