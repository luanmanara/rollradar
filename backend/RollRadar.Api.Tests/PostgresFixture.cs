using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Data;
using Testcontainers.PostgreSql;

namespace RollRadar.Api.Tests;

/// <summary>
/// A throwaway PostgreSQL in Docker (same image as docker-compose.yml) with the migrations applied, shared by
/// the database tests. Needs Docker running.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    public string ConnectionString => _container.GetConnectionString();

    public RollRadarDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RollRadarDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Empties every table, so each test starts from its own data.</summary>
    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE athletes, api_tokens, moment_votes, technique_moments, transcript_segments, technique_aliases, techniques, videos, channels RESTART IDENTITY CASCADE");
    }
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
