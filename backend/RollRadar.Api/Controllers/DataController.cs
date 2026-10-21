using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Auth;
using RollRadar.Api.Curation;
using RollRadar.Api.Data;
using RollRadar.Api.Models;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Controllers;

/// <summary>
/// The collected data for external clients: techniques, videos, moments with their curation and full transcripts.
/// Needs a bearer token with the "leitura" scope (created with the collector's "token criar").
/// </summary>
[ApiController]
[Route("api/v1/dados")]
[Authorize(Policy = ReadPolicy)]
[EnableRateLimiting(RateLimitPolicy)]
public class DataController(RollRadarDbContext db, TechniqueCatalog catalog) : ControllerBase
{
    public const string ReadPolicy = "dados-leitura";
    public const string RateLimitPolicy = "dados";
    public const int DefaultLimit = 100;
    public const int MaxLimit = 1000;

    private static readonly Dictionary<string, Data.CurationStatus> Statuses =
        Enum.GetValues<Data.CurationStatus>().ToDictionary(s => SnakeCase.From(s.ToString()));

    [HttpGet("techniques")]
    public ActionResult<IReadOnlyList<DataTechnique>> Techniques() =>
        Ok(catalog.All.Select(t => new DataTechnique(t.Id, t.English, t.Portuguese, t.Category, t.EnglishTerms, t.Aliases)).ToList());

    /// <summary>Videos ordered by id.</summary>
    [HttpGet("videos")]
    public async Task<ActionResult<DataPage<DataVideo>>> Videos([FromQuery] string? after, [FromQuery] int limit = DefaultLimit)
    {
        var take = Math.Clamp(limit, 1, MaxLimit);
        var query = db.Videos.AsNoTracking();
        if (!string.IsNullOrEmpty(after)) query = query.Where(v => string.Compare(v.Id, after) > 0);

        var videos = await query
            .OrderBy(v => v.Id)
            .Take(take)
            .Select(v => new DataVideo(
                v.Id, v.Title, "https://www.youtube.com/watch?v=" + v.Id, v.Channel!.Name, v.UploadDate, v.DurationSeconds,
                v.Language, v.CaptionStatus == CaptionStatus.Legenda, v.Moments.Count))
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(new DataPage<DataVideo>(videos, videos.Count == take ? videos[^1].Id : null));
    }

    [HttpGet("videos/{videoId}/transcript")]
    public async Task<ActionResult<DataTranscript>> Transcript(string videoId)
    {
        if (!await db.Videos.AnyAsync(v => v.Id == videoId, HttpContext.RequestAborted)) return NotFound();

        var segments = await db.TranscriptSegments
            .AsNoTracking()
            .Where(s => s.VideoId == videoId)
            .OrderBy(s => s.StartSeconds)
            .ThenBy(s => s.Id)
            .Select(s => new DataTranscriptSegment(s.StartSeconds, s.DurationSeconds, s.Text))
            .ToListAsync(HttpContext.RequestAborted);
        return Ok(new DataTranscript(videoId, segments));
    }

    /// <summary>
    /// Moments ordered as stored, optionally of one technique (catalog id or any name the site understands), one
    /// video and/or one curation status (pendente, bom, ruim).
    /// </summary>
    [HttpGet("moments")]
    public async Task<ActionResult<DataPage<DataMoment>>> Moments(
        [FromQuery] string? technique, [FromQuery] string? video, [FromQuery] string? status,
        [FromQuery] string? after, [FromQuery] int limit = DefaultLimit)
    {
        var take = Math.Clamp(limit, 1, MaxLimit);
        var query = db.TechniqueMoments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(technique))
        {
            var ids = catalog.Resolve(technique).Select(t => t.Id).Append(technique.Trim()).ToList();
            query = query.Where(m => ids.Contains(m.TechniqueId));
        }
        if (!string.IsNullOrWhiteSpace(video)) query = query.Where(m => m.VideoId == video);
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Statuses.TryGetValue(status, out var value)) return BadRequest($"Unknown status: {status}");
            query = query.Where(m => m.CurationStatus == value);
        }
        if (!string.IsNullOrEmpty(after))
        {
            if (!long.TryParse(after, NumberStyles.None, CultureInfo.InvariantCulture, out var afterId))
                return BadRequest($"Invalid cursor: {after}");
            query = query.Where(m => m.Id > afterId);
        }

        var moments = await query.OrderBy(m => m.Id).Take(take).ToListAsync(HttpContext.RequestAborted);
        var momentIds = moments.Select(m => m.Id).ToList();
        var votes = (await db.MomentVotes.AsNoTracking()
                .Where(v => momentIds.Contains(v.MomentId))
                .ToListAsync(HttpContext.RequestAborted))
            .ToLookup(v => v.MomentId);

        var items = moments.Select(m =>
        {
            var tally = VoteTally.From(votes[m.Id].Select(v =>
                new MomentVote("", v.VoterId, v.IpHash, v.Useful, new DateTimeOffset(DateTime.SpecifyKind(v.VotedAt, DateTimeKind.Utc)))));
            return new DataMoment(
                MomentKey.For(m.VideoId, m.TechniqueId, m.Term, m.StartSeconds), m.VideoId, m.TechniqueId, m.StartSeconds,
                m.Term, m.Snippet, SnakeCase.From(m.CurationStatus.ToString()), m.CuratedAt, tally.Useful, tally.NotUseful);
        }).ToList();
        var next = moments.Count == take ? moments[^1].Id.ToString(CultureInfo.InvariantCulture) : null;
        return Ok(new DataPage<DataMoment>(items, next));
    }
}
