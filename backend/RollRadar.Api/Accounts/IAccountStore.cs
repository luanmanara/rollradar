namespace RollRadar.Api.Accounts;

/// <summary>What a login provider tells about the person signing in.</summary>
/// <param name="Provider">E.g. "Google".</param>
/// <param name="ProviderKey">The provider's stable id of the account (Google "sub").</param>
public record ExternalLogin(string Provider, string ProviderKey, string? Email, string? DisplayName);

/// <summary>A user account as the site shows it.</summary>
public record Account(Guid Id, string? Email, string? DisplayName);

/// <summary>User accounts and the external logins they sign in with. Named to stay clear of Identity's IUserStore.</summary>
public interface IAccountStore
{
    /// <summary>
    /// Finds the user of the login, creating it on the first sign-in, and records the latest email, name and
    /// sign-in time. Returns the user's id.
    /// </summary>
    Task<Guid> SignInAsync(ExternalLogin login, CancellationToken cancellationToken = default);

    /// <summary>Null when there is no such user (e.g. it was deleted after the cookie was issued).</summary>
    Task<Account?> FindAsync(Guid id, CancellationToken cancellationToken = default);
}
