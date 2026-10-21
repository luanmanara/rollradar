using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RollRadar.Api.Curation;
using RollRadar.Api.Services;

namespace RollRadar.Api.Controllers;

/// <summary>Anonymous votes on whether a moment really shows the technique. No account needed.</summary>
[ApiController]
[Route("api/moments/{momentId}/vote")]
[AllowAnonymous]
public class MomentsController(IVideoSearchService searchService, ICurationStore store, VoterIdentity voters) : ControllerBase
{
    public const string RateLimitPolicy = "votes";

    [HttpGet]
    public ActionResult<MyVote> Get(string momentId)
    {
        var voter = voters.Find(HttpContext);
        return Ok(new MyVote(voter is null ? null : store.GetVote(momentId, voter)?.Useful));
    }

    /// <summary>Records the vote, replacing the visitor's earlier vote on the same moment.</summary>
    [HttpPut]
    [EnableRateLimiting(RateLimitPolicy)]
    public ActionResult<MyVote> Vote(string momentId, VoteRequest request)
    {
        if (!searchService.MomentExists(momentId)) return NotFound();

        var voter = voters.GetOrCreate(HttpContext);
        store.SaveVote(new MomentVote(momentId, voter, voters.HashIp(HttpContext), request.Useful, DateTimeOffset.UtcNow));
        return Ok(new MyVote(request.Useful));
    }

    [HttpDelete]
    [EnableRateLimiting(RateLimitPolicy)]
    public IActionResult Remove(string momentId)
    {
        if (voters.Find(HttpContext) is { } voter) store.RemoveVote(momentId, voter);
        return NoContent();
    }
}
