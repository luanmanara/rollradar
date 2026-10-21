using System.Globalization;
using System.Text.Json;
using RollRadar.Api.Common;
using RollRadar.Api.Models;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Transcripts;

/// <summary>
/// Loads the per-technique files written by montar_tecnicas.py (saida/&lt;technique-id&gt;.json, format in saida/FORMATO.md).
/// The file name is the catalog technique id; files that are not technique files, like indice.json, are skipped.
/// </summary>
public static class TechniqueFileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record VideoEntry(string? ChannelName, string? Id, string? Title, string? UploadDate, string? Lang, List<HitEntry>? Hits);

    /// <param name="Termo">Term found, normalized. Missing in files from older versions of the script.</param>
    private record HitEntry(double Start, string? Termo, string? Text);

    public static IReadOnlyList<FightVideo> Load(IEnumerable<string> jsonFiles, TechniqueCatalog catalog, ILogger? logger = null) =>
        MomentMerger.MergeVideos(jsonFiles.SelectMany(file => LoadFile(file, catalog, logger)));

    private static IEnumerable<FightVideo> LoadFile(string file, TechniqueCatalog catalog, ILogger? logger)
    {
        List<VideoEntry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<VideoEntry>>(File.ReadAllText(file), JsonOptions);
        }
        catch (JsonException)
        {
            entries = null;
        }

        if (entries is null || entries.Any(e => e.Id is null || e.Hits is null))
        {
            logger?.LogDebug("Skipping {File}: not a technique file", Path.GetFileName(file));
            return [];
        }

        var techniqueId = Path.GetFileNameWithoutExtension(file);
        var technique = catalog.FindById(techniqueId);
        if (technique is null)
            logger?.LogWarning("{File}: technique id not in the catalog, searchable only by its file name", Path.GetFileName(file));

        return entries.Select(e => new FightVideo(
            e.Id!,
            e.Title ?? "",
            $"https://www.youtube.com/watch?v={e.Id}",
            $"https://i.ytimg.com/vi/{e.Id}/mqdefault.jpg",
            e.ChannelName ?? "",
            DateOnly.TryParseExact(e.UploadDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
            e.Hits!.Select(h => new TechniqueMoment(
                h.Termo ?? technique?.English ?? techniqueId,
                (int)Math.Floor(h.Start),
                h.Text?.Trim() ?? "",
                technique?.Id)).ToList(),
            e.Lang));
    }
}
