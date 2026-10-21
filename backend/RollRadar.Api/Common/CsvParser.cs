using System.Text;

namespace RollRadar.Api.Common;

public static class CsvParser
{
    /// <summary>
    /// RFC 4180 parser: quoted fields may contain commas, quotes ("") and line breaks.
    /// Returns the header row first. Blank lines and a leading BOM are ignored.
    /// </summary>
    public static List<List<string>> Parse(string text)
    {
        text = text.TrimStart('﻿');
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != '"') field.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else inQuotes = false;
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                record.Add(field.ToString());
                field.Clear();
                if (record.Count > 1 || record[0].Length > 0) records.Add(record);
                record = [];
            }
            else field.Append(c);
        }

        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }

        return records;
    }

    /// <summary>Lower-cased header names, failing when any required column is missing.</summary>
    public static List<string> RequireColumns(List<string> header, IEnumerable<string> required, string fileDescription)
    {
        var names = header.Select(h => h.Trim().ToLowerInvariant()).ToList();
        var missing = required.Where(c => !names.Contains(c)).ToList();
        if (missing.Count > 0)
            throw new FormatException($"Not a {fileDescription}, missing columns: {string.Join(", ", missing)}");
        return names;
    }
}
