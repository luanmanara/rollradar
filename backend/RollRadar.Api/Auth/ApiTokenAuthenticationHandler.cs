using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RollRadar.Api.Data;

namespace RollRadar.Api.Auth;

/// <summary>
/// Accepts "Authorization: Bearer rr_..." when the token's hash is in api_tokens, not revoked and not expired.
/// Without the header the request stays anonymous, so the site's open endpoints keep working.
/// </summary>
public class ApiTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, RollRadarDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "ApiToken";
    public const string ScopeClaim = "scope";

    /// <summary>last_used_at is written at most this often per token, not on every request.</summary>
    private static readonly TimeSpan LastUsedPrecision = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return AuthenticateResult.NoResult();

        var token = header["Bearer ".Length..].Trim();
        if (token.Length == 0) return AuthenticateResult.Fail("Empty token");

        var hash = ApiTokens.Hash(token);
        var now = DateTime.UtcNow;
        var found = await db.ApiTokens.AsNoTracking().FirstOrDefaultAsync(t => t.TokenHash == hash, Context.RequestAborted);
        if (found is null) return AuthenticateResult.Fail("Unknown token");
        if (found.RevokedAt is not null) return AuthenticateResult.Fail("Revoked token");
        if (found.ExpiresAt <= now) return AuthenticateResult.Fail("Expired token");

        if (found.LastUsedAt is null || now - found.LastUsedAt > LastUsedPrecision)
            await db.ApiTokens.Where(t => t.Id == found.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, now), Context.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, found.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, found.Name),
        };
        claims.AddRange(found.Scopes.Select(s => new Claim(ScopeClaim, s)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
