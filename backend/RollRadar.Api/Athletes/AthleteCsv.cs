using RollRadar.Api.Common;

namespace RollRadar.Api.Athletes;

/// <summary>A row of the athletes CSV.</summary>
/// <param name="Fights">Fights with moments that name the athlete; informative, not imported.</param>
/// <param name="Suggested">A name read from the titles that is not in the catalog yet; informative, not imported.</param>
public record AthleteRow(Athlete Athlete, int Fights = 0, bool Suggested = false);

/// <summary>
/// The athletes CSV edited by hand. Columns: nome, apelidos (comma separated), lutas, novo ("sim" for names
/// suggested from the titles). Importing reads only nome and apelidos: every row becomes an athlete.
/// </summary>
public static class AthleteCsv
{
    private static readonly string[] RequiredColumns = ["nome", "apelidos"];

    public static IReadOnlyList<Athlete> Read(TextReader reader)
    {
        var records = CsvParser.Parse(reader.ReadToEnd());
        if (records.Count == 0) return [];

        var header = CsvParser.RequireColumns(records[0], RequiredColumns, "athletes CSV");
        int Col(string name) => header.IndexOf(name);

        return records
            .Skip(1)
            .Where(r => r.Count == header.Count && r[Col("nome")].Trim().Length > 0)
            .Select(r => new Athlete(
                r[Col("nome")].Trim(),
                r[Col("apelidos")].Split([",", ";"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()))
            .ToList();
    }

    public static void Write(IEnumerable<AthleteRow> rows, TextWriter writer)
    {
        writer.WriteLine("nome,apelidos,lutas,novo");
        foreach (var row in rows)
            writer.WriteLine(string.Join(",",
                Field(row.Athlete.Name), Field(string.Join(", ", row.Athlete.Aliases)), row.Fights, row.Suggested ? "sim" : ""));
    }

    private static string Field(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0 ? value : $"\"{value.Replace("\"", "\"\"")}\"";
}
