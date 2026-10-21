using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace RollRadar.Api.Curation;

/// <summary>
/// Identifies anonymous voters: a random id in a cookie signed by the server (so ids can't be made up),
/// plus a keyed hash of the IP address (so clearing the cookie doesn't count twice).
/// </summary>
public class VoterIdentity
{
    public const string CookieName = "rr_voter";

    private readonly IDataProtector _protector;
    private readonly byte[] _ipHashKey;

    public VoterIdentity(IDataProtectionProvider dataProtection, IOptions<CurationOptions> options, ILogger<VoterIdentity> logger)
    {
        _protector = dataProtection.CreateProtector("RollRadar.Curation.Voter");
        var key = options.Value.IpHashKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            logger.LogWarning("Curation:IpHashKey is not set; using a random key until the API restarts.");
            _ipHashKey = RandomNumberGenerator.GetBytes(32);
        }
        else
        {
            _ipHashKey = Encoding.UTF8.GetBytes(key);
        }
    }

    /// <summary>The voter id in the request's cookie, or null when there is none or it was tampered with.</summary>
    public string? Find(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie)) return null;
        try
        {
            return _protector.Unprotect(cookie);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>The voter id in the cookie, creating one (and the cookie) on the first vote.</summary>
    public string GetOrCreate(HttpContext context)
    {
        if (Find(context) is { } id) return id;

        id = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(CookieName, _protector.Protect(id), new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(365)
        });
        return id;
    }

    /// <summary>
    /// Keyed hash of the client's IP address. Behind a reverse proxy, the forwarded headers middleware must be
    /// set up so this is the visitor's address and not the proxy's.
    /// </summary>
    public string HashIp(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return Convert.ToHexString(HMACSHA256.HashData(_ipHashKey, Encoding.UTF8.GetBytes(ip)))[..32];
    }
}
