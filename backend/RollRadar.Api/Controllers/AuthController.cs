using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RollRadar.Api.Accounts;
using RollRadar.Api.Training;

namespace RollRadar.Api.Controllers;

/// <summary>Sign-in with Google and the signed-in user's account. The session lives in the rr_session cookie.</summary>
[ApiController]
[Route("api")]
public class AuthController(IAccountStore accounts, IAuthenticationSchemeProvider schemes) : ControllerBase
{
    /// <summary>
    /// Sends the browser to Google; after the sign-in it comes back to <paramref name="returnUrl"/> (only addresses
    /// of this site, anything else becomes "/"). 503 when the API has no Google client configured.
    /// </summary>
    [HttpGet("auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl)
    {
        if (await schemes.GetSchemeAsync(UserAuthentication.GoogleScheme) is null)
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Login com Google indisponível",
                detail: $"A API não tem {UserAuthentication.ClientIdKey} e {UserAuthentication.ClientSecretKey} configurados.");

        var properties = new AuthenticationProperties
        {
            RedirectUri = UserAuthentication.SafeReturnUrl(returnUrl),
            IsPersistent = true,
        };
        return Challenge(properties, UserAuthentication.GoogleScheme);
    }

    [HttpPost("auth/logout")]
    [Authorize(Policy = UserAuthentication.Policy)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(UserAuthentication.CookieScheme);
        return NoContent();
    }

    /// <summary>The signed-in user; 401 when not signed in.</summary>
    [HttpGet("eu")]
    [Authorize(Policy = UserAuthentication.Policy)]
    public async Task<ActionResult<Me>> GetMe(CancellationToken cancellationToken)
    {
        var account = User.FindUserId() is { } id ? await accounts.FindAsync(id, cancellationToken) : null;
        if (account is null)
        {
            // Cookie of a user that no longer exists: end the session.
            await HttpContext.SignOutAsync(UserAuthentication.CookieScheme);
            return Unauthorized();
        }
        return new Me(account.Id, account.Email, account.DisplayName);
    }

    /// <summary>Everything the user recorded on the site, as a JSON file to download.</summary>
    [HttpGet("conta/exportar")]
    [Authorize(Policy = UserAuthentication.Policy)]
    public async Task<IActionResult> Export([FromServices] ITrainingStore training, CancellationToken cancellationToken)
    {
        if (User.FindUserId() is not { } id || await training.ExportAsync(id, cancellationToken) is not { } export) return Unauthorized();
        Response.Headers.ContentDisposition = $"attachment; filename=\"rollradar-dados-{DateTime.UtcNow:yyyy-MM-dd}.json\"";
        return Ok(export);
    }

    /// <summary>Deletes the account and all its data for good, and ends the session. The site asks for confirmation first.</summary>
    [HttpDelete("conta")]
    [Authorize(Policy = UserAuthentication.Policy)]
    public async Task<IActionResult> DeleteAccount(CancellationToken cancellationToken)
    {
        if (User.FindUserId() is { } id) await accounts.DeleteAsync(id, cancellationToken);
        await HttpContext.SignOutAsync(UserAuthentication.CookieScheme);
        return NoContent();
    }
}

/// <summary>The signed-in user, as GET /api/eu returns it.</summary>
public record Me(Guid Id, string? Email, string? Nome);
