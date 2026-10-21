using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollRadar.Api.Curation;
using RollRadar.Api.Services;

namespace RollRadar.Api.Controllers;

/// <summary>
/// The curator's review of the moments, guided by the visitors' votes. Needs the curator key (not a bearer token:
/// the curation page of the site sends <see cref="CuratorKeyFilter.Header"/>).
/// </summary>
[ApiController]
[Route("api/curation")]
[AllowAnonymous]
[TypeFilter(typeof(CuratorKeyFilter))]
public class CurationController(IVideoSearchService searchService, ICurationStore store) : ControllerBase
{
    /// <summary>Any status, for <c>status=todos</c>.</summary>
    public const string AllStatuses = "todos";

    /// <summary>Answers 204 when the curator key is right, so the page can check it.</summary>
    [HttpGet]
    public IActionResult Check() => NoContent();

    /// <summary>
    /// Moments of the technique (all techniques when empty) with the given status, the most voted first and,
    /// among those, the ones with more "not useful" votes first.
    /// </summary>
    [HttpGet("queue")]
    public ActionResult<IReadOnlyList<CurationItem>> Queue(
        [FromQuery] string? technique, [FromQuery] string status = CurationStatus.Pending, [FromQuery] int limit = 100)
    {
        if (status != AllStatuses && !CurationStatus.IsValid(status)) return BadRequest($"Unknown status: {status}");

        var items = searchService.Search(technique, null, includeRejected: true).Videos
            .SelectMany(v => v.Moments.Select(m => new CurationItem(v with { Moments = [m] }, m, store.GetTally(m.Id), store.GetStatus(m.Id))))
            .Where(i => status == AllStatuses || i.Status == status)
            .OrderByDescending(i => i.Votes.Total)
            .ThenByDescending(i => i.Votes.NotUseful)
            .ThenByDescending(i => i.Video.UploadDate)
            .ThenBy(i => i.Moment.StartSeconds)
            .Take(Math.Clamp(limit, 1, 500))
            .ToList();
        return Ok(items);
    }

    [HttpPut("moments/{momentId}/status")]
    public IActionResult SetStatus(string momentId, StatusRequest request)
    {
        if (!CurationStatus.IsValid(request.Status)) return BadRequest($"Unknown status: {request.Status}");
        if (!searchService.MomentExists(momentId)) return NotFound();

        store.SetStatus(momentId, request.Status);
        return NoContent();
    }
}
