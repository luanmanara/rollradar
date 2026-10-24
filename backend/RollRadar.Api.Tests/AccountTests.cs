using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RollRadar.Api.Accounts;
using RollRadar.Api.Controllers;
using RollRadar.Api.Data;

namespace RollRadar.Api.Tests;

/// <summary>User accounts: the store over a real database, and the login, logout and /api/eu endpoints.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountTests(PostgresFixture database) : IAsyncLifetime
{
    private static readonly ExternalLogin Ana = new("Google", "google-sub-1", "ana@exemplo.com", "Ana Souza");

    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A new context per call, like one request each in the API.</summary>
    private async Task<T> With<T>(Func<DbAccountStore, Task<T>> action)
    {
        await using var db = database.CreateContext();
        return await action(new DbAccountStore(db));
    }

    private WebApplicationFactory<Program> Api(bool withGoogle = false) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing").UseSetting("ConnectionStrings:RollRadar", database.ConnectionString);
            if (!withGoogle) return;
            builder.UseSetting(UserAuthentication.ClientIdKey, "client-de-teste")
                .UseSetting(UserAuthentication.ClientSecretKey, "segredo-de-teste")
                // Shows where the browser comes back to after Google, which is otherwise encrypted in "state".
                .ConfigureTestServices(services => services.PostConfigure<GoogleOptions>(UserAuthentication.GoogleScheme, options =>
                    options.Events.OnRedirectToAuthorizationEndpoint = context =>
                    {
                        context.Response.Headers["X-Return-Url"] = context.Properties.RedirectUri;
                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    }));
        });

    private static HttpClient Client(WebApplicationFactory<Program> api) =>
        api.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>The rr_session cookie the API would issue after Google signs the user in.</summary>
    private static string SessionCookie(WebApplicationFactory<Program> api, Guid userId)
    {
        var options = api.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(UserAuthentication.CookieScheme);
        var identity = new ClaimsIdentity([new Claim(UserAuthentication.UserIdClaim, userId.ToString())], UserAuthentication.CookieScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), UserAuthentication.CookieScheme);
        return $"{UserAuthentication.CookieName}={options.TicketDataFormat.Protect(ticket)}";
    }

    [Fact]
    public async Task The_first_sign_in_creates_the_user_and_the_next_ones_reuse_it()
    {
        var first = await With(s => s.SignInAsync(Ana));
        var second = await With(s => s.SignInAsync(Ana with { Email = "ana@novo.com", DisplayName = "Ana S." }));

        Assert.Equal(first, second);
        Assert.Equal(new Account(first, "ana@novo.com", "Ana S."), await With(s => s.FindAsync(first)));

        await using var db = database.CreateContext();
        var user = await db.Users.Include(u => u.Logins).SingleAsync();
        Assert.Equal(("Google", "google-sub-1"), (user.Logins.Single().Provider, user.Logins.Single().ProviderKey));
        Assert.True(user.LastLoginAt >= user.CreatedAt);
    }

    [Fact]
    public async Task Another_account_is_another_user()
    {
        var ana = await With(s => s.SignInAsync(Ana));
        var bia = await With(s => s.SignInAsync(new ExternalLogin("Google", "google-sub-2", "bia@exemplo.com", "Bia")));

        Assert.NotEqual(ana, bia);
        Assert.Null(await With(s => s.FindAsync(Guid.NewGuid())));
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("/", "/")]
    [InlineData("/tecnicas?tecnica=armlock", "/tecnicas?tecnica=armlock")]
    [InlineData("~/curadoria", "/curadoria")]
    [InlineData("https://malicioso.com/", "/")]
    [InlineData("//malicioso.com", "/")]
    [InlineData("/\\malicioso.com", "/")]
    [InlineData("javascript:alert(1)", "/")]
    public void Only_addresses_of_the_site_are_kept_as_return_url(string? returnUrl, string expected) =>
        Assert.Equal(expected, UserAuthentication.SafeReturnUrl(returnUrl));

    [Fact]
    public async Task Without_a_session_me_and_logout_answer_401()
    {
        await using var api = Api();
        var client = Client(api);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/eu")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task Without_the_Google_client_login_answers_503()
    {
        await using var api = Api();

        var response = await Client(api).GetAsync("/api/auth/login?returnUrl=/");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(UserAuthentication.ClientIdKey, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/tecnicas?tecnica=armlock", "/tecnicas?tecnica=armlock")]
    [InlineData("https://malicioso.com/", "/")]
    public async Task Login_sends_the_browser_to_Google_and_back_to_the_site(string returnUrl, string expected)
    {
        await using var api = Api(withGoogle: true);

        var response = await Client(api).GetAsync("/api/auth/login?returnUrl=" + Uri.EscapeDataString(returnUrl));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith("https://accounts.google.com/", location);
        Assert.Contains("redirect_uri=" + Uri.EscapeDataString("http://localhost/api/auth/google-callback"), location);
        Assert.Equal(expected, response.Headers.GetValues("X-Return-Url").Single());
    }

    [Fact]
    public async Task With_a_session_me_returns_the_user_and_logout_ends_it()
    {
        var id = await With(s => s.SignInAsync(Ana));
        await using var api = Api();
        var client = Client(api);
        client.DefaultRequestHeaders.Add("Cookie", SessionCookie(api, id));

        var me = await client.GetFromJsonAsync<Me>("/api/eu");
        Assert.Equal(new Me(id, "ana@exemplo.com", "Ana Souza"), me);

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var cleared = logout.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(UserAuthentication.CookieName + "="));
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared);
    }

    [Fact]
    public async Task A_session_of_a_deleted_user_is_refused()
    {
        await using var api = Api();
        var client = Client(api);
        client.DefaultRequestHeaders.Add("Cookie", SessionCookie(api, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/eu")).StatusCode);
    }

    [Fact]
    public async Task The_session_cookie_does_not_open_the_data_api()
    {
        var id = await With(s => s.SignInAsync(Ana));
        await using var api = Api();
        var client = Client(api);
        client.DefaultRequestHeaders.Add("Cookie", SessionCookie(api, id));

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/dados/techniques")).StatusCode);
    }
}
