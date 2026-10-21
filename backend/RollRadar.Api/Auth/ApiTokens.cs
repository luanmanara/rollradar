using System.Security.Cryptography;
using System.Text;

namespace RollRadar.Api.Auth;

/// <summary>
/// Bearer tokens for external clients of the data API. A token is "rr_" plus 32 random bytes (base64url); only its
/// SHA-256 is stored (api_tokens.token_hash), so a leaked database does not leak working tokens.
/// </summary>
public static class ApiTokens
{
    public const string Prefix = "rr_";

    /// <summary>Characters of the token kept in api_tokens.token_start, so it can be recognized in a list.</summary>
    public const int VisibleLength = 8;

    public static string Generate() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>SHA-256 of the token as 64 lowercase hex characters.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    public static string Start(string token) => token[..Math.Min(VisibleLength, token.Length)];
}

/// <summary>What a token allows. Kept as strings in api_tokens.scopes.</summary>
public static class ApiScopes
{
    /// <summary>Reads the data endpoints under /api/v1/dados.</summary>
    public const string Read = "leitura";

    public static readonly IReadOnlyList<string> All = [Read];

    public static bool IsValid(string scope) => All.Contains(scope);
}
