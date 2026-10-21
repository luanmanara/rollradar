using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RollRadar.Api.Athletes;
using RollRadar.Api.Auth;
using RollRadar.Api.Controllers;
using RollRadar.Api.Curation;
using RollRadar.Api.Data;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // "Authorize" button for the /api/v1/dados endpoints.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        Description = "Token criado com: dotnet run --project backend/RollRadar.Coletor -- token criar <nome>",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
});

// Password comes from user-secrets (ConnectionStrings:RollRadar), never from appsettings.
builder.Services.AddDbContext<RollRadarDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("RollRadar"))
    .UseSnakeCaseNamingConvention());

// Data comes from the database (filled by RollRadar.Coletor); the catalog is read once, on first use.
builder.Services.AddSingleton(sp =>
{
    using var scope = sp.CreateScope();
    var catalog = DbTechniqueCatalog.Load(scope.ServiceProvider.GetRequiredService<RollRadarDbContext>());
    sp.GetRequiredService<ILogger<TechniqueCatalog>>().LogInformation("Loaded {Count} techniques from the database", catalog.All.Count);
    return catalog;
});
builder.Services.AddSingleton(sp =>
{
    using var scope = sp.CreateScope();
    var athletes = DbAthleteCatalog.Load(scope.ServiceProvider.GetRequiredService<RollRadarDbContext>());
    sp.GetRequiredService<ILogger<AthleteCatalog>>().LogInformation("Loaded {Count} athletes from the database", athletes.All.Count);
    return athletes;
});
builder.Services.AddScoped<IVideoSearchService, DbVideoSearchService>();

builder.Services.Configure<CurationOptions>(builder.Configuration.GetSection(CurationOptions.Section));
builder.Services.AddDataProtection();
builder.Services.AddSingleton<VoterIdentity>();
builder.Services.AddScoped<ICurationStore, EfCurationStore>();

// External clients read the data with a bearer token (api_tokens). The site's endpoints opt out with
// [AllowAnonymous]; anything else needs a valid token, so a new endpoint is never open by accident.
builder.Services.Configure<ApiAccessOptions>(builder.Configuration.GetSection(ApiAccessOptions.Section));
builder.Services.AddAuthentication(ApiTokenAuthenticationHandler.Scheme)
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>(ApiTokenAuthenticationHandler.Scheme, null);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder(ApiTokenAuthenticationHandler.Scheme).RequireAuthenticatedUser().Build())
    .AddPolicy(DataController.ReadPolicy, policy => policy
        .AddAuthenticationSchemes(ApiTokenAuthenticationHandler.Scheme)
        .RequireClaim(ApiTokenAuthenticationHandler.ScopeClaim, ApiScopes.Read));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(DataController.RateLimitPolicy, context =>
    {
        var perMinute = context.RequestServices.GetRequiredService<IOptions<ApiAccessOptions>>().Value.TokenRequestsPerMinute;
        // Per token; requests without a valid token are rejected by authorization anyway, limited per IP until then.
        var key = context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } tokenId
            ? "token:" + tokenId
            : "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        return RateLimitPartition.GetFixedWindowLimiter(
            key, _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) });
    });
    options.AddPolicy(VideosController.RateLimitPolicy, context =>
    {
        var perMinute = context.RequestServices.GetRequiredService<IOptions<ApiAccessOptions>>().Value.SiteRequestsPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = perMinute, Window = TimeSpan.FromMinutes(1) });
    });
    options.AddPolicy(MomentsController.RateLimitPolicy, context =>
    {
        var votesPerMinute = context.RequestServices.GetRequiredService<IOptions<CurationOptions>>().Value.VotesPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = votesPerMinute, Window = TimeSpan.FromMinutes(1) });
    });
});

builder.Services.AddCors(options =>
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Lets the tests start the API with WebApplicationFactory.
public partial class Program;
