using RollRadar.Api.Models;
using RollRadar.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using RollRadar.Api.Auth;

namespace RollRadar.Api.Controllers;

/// <summary>The site's search. Open (no token), limited per IP address; external clients use <see cref="DataController"/>.</summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicy)]
public class VideosController(IVideoSearchService searchService, IOptions<ApiAccessOptions> options) : ControllerBase
{
    public const string RateLimitPolicy = "site";

    /// <summary>Techniques searched together at most; more are ignored.</summary>
    public const int MaxTechniques = 3;

    /// <summary>
    /// Search fight videos by technique (in Portuguese or English) and/or athlete (matched against the title).
    /// Repeating the technique (?technique=armbar&amp;technique=triangulo) searches up to <see cref="MaxTechniques"/> together.
    /// </summary>
    [HttpGet]
    public ActionResult<SearchResult> Search([FromQuery] string[]? technique, [FromQuery] string? athlete)
    {
        var techniques = (technique ?? []).Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxTechniques).ToList();
        if (techniques.Count > 1) return Ok(searchService.SearchMany(techniques, athlete));

        var result = searchService.Search(techniques.FirstOrDefault(), athlete);
        if (techniques.Count > 0) return Ok(result);

        // Without a technique, only the most recent videos: the whole data set is for token holders (DataController).
        var recent = result.Videos
            .OrderByDescending(v => v.UploadDate)
            .ThenBy(v => v.Id, StringComparer.Ordinal)
            .Take(options.Value.UnfilteredSearchMaxVideos)
            .ToList();
        return Ok(result with { Videos = recent });
    }

    /// <summary>
    /// Athletes found in the fight titles, most fights first, for the search box. Searching by one of them is
    /// this search with only the athlete: their most recent fights, with every technique.
    /// </summary>
    [HttpGet("athletes")]
    public ActionResult<IReadOnlyList<AthleteSummary>> Athletes() => Ok(searchService.GetAthletes());

    /// <summary>Catalog techniques with how many loaded videos mention each one.</summary>
    [HttpGet("techniques")]
    public ActionResult<IReadOnlyList<TechniqueSummary>> Techniques() => Ok(searchService.GetTechniques());
}
