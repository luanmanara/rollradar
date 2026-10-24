using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;

namespace RollRadar.Api.Accounts;

/// <summary>
/// Site accounts: Google signs the person in, then our own cookie (rr_session) keeps them signed in. The cookie
/// holds the user id in <see cref="UserIdClaim"/>; endpoints of the account use the <see cref="Policy"/> policy.
/// </summary>
public static class UserAuthentication
{
    public const string CookieScheme = "UserCookie";
    public const string CookieName = "rr_session";
    public const string GoogleScheme = GoogleDefaults.AuthenticationScheme;
    public const string Policy = "User";

    /// <summary>Id of the row in users.</summary>
    public const string UserIdClaim = "rr_user_id";

    public const string CallbackPath = "/api/auth/google-callback";
    public const string ClientIdKey = "Auth:Google:ClientId";
    public const string ClientSecretKey = "Auth:Google:ClientSecret";

    /// <summary>
    /// Adds the session cookie and, when <see cref="ClientIdKey"/> and <see cref="ClientSecretKey"/> are set
    /// (user-secrets, never appsettings), Google. Without them the API runs and /api/auth/login answers 503.
    /// </summary>
    public static AuthenticationBuilder AddUserAccounts(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        builder.AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            // An API: answer with the status code, the site decides where to go.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        var clientId = configuration[ClientIdKey];
        var clientSecret = configuration[ClientSecretKey];
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret)) return builder;

        return builder.AddGoogle(GoogleScheme, options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = clientSecret;
            options.SignInScheme = CookieScheme;
            options.CallbackPath = CallbackPath;
            // Google comes back with a top-level GET, so Lax is enough, and it also works on http://localhost.
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.Events.OnCreatingTicket = async context =>
            {
                var principal = context.Principal!;
                var providerKey = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? throw new InvalidOperationException("Google did not send the account id.");
                var accounts = context.HttpContext.RequestServices.GetRequiredService<IAccountStore>();
                var userId = await accounts.SignInAsync(
                    new ExternalLogin(GoogleScheme, providerKey, principal.FindFirstValue(ClaimTypes.Email), principal.FindFirstValue(ClaimTypes.Name)),
                    context.HttpContext.RequestAborted);
                context.Identity!.AddClaim(new Claim(UserIdClaim, userId.ToString()));
            };
        });
    }

    public static AuthorizationBuilder AddUserPolicy(this AuthorizationBuilder builder) =>
        builder.AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(CookieScheme)
            .RequireClaim(UserIdClaim));

    /// <summary>The signed-in user's id, or null.</summary>
    public static Guid? FindUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(UserIdClaim), out var id) ? id : null;

    /// <summary>
    /// The return address when it stays on this site ("/tecnicas?x=1"), otherwise "/". Same rule as
    /// IUrlHelper.IsLocalUrl: one leading slash, not "//" or "/\" (another host), or "~/".
    /// </summary>
    public static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl)) return "/";
        if (returnUrl[0] == '/')
        {
            if (returnUrl.Length == 1) return returnUrl;
            if (returnUrl[1] != '/' && returnUrl[1] != '\\' && !returnUrl.Any(char.IsControl)) return returnUrl;
            return "/";
        }
        if (returnUrl.Length > 1 && returnUrl[0] == '~' && returnUrl[1] == '/')
            return SafeReturnUrl(returnUrl[1..]);
        return "/";
    }
}
