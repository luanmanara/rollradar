using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RollRadar.Api.Data;

namespace RollRadar.Api.Tests;

/// <summary>The migration that added the game map turns submissions already recorded into the map's submission arrows.</summary>
[Collection(PostgresCollection.Name)]
public sealed class GameMapMigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task Submissions_with_position_and_technique_become_one_arrow_each()
    {
        var connection = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = "rr_migration_" + Guid.NewGuid().ToString("N")[..8],
        }.ConnectionString;
        await using var db = new RollRadarDbContext(new DbContextOptionsBuilder<RollRadarDbContext>()
            .UseNpgsql(connection).UseSnakeCaseNamingConvention().Options);
        var migrator = db.GetService<IMigrator>();
        try
        {
            await migrator.MigrateAsync(db.Database.GetMigrations().Single(m => m.EndsWith("_RollFinishes")));
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO users (id, email) VALUES ('11111111-1111-1111-1111-111111111111', 'ana@exemplo.com');
                INSERT INTO techniques (id, english, portuguese, category) VALUES
                    ('armbar', 'Armbar', 'Chave de braço', 'Chaves de braço'),
                    ('mount', 'Mount', 'Montada', 'Posições');
                INSERT INTO game_nodes (id, user_id, technique_id, side, hidden, created_at)
                    VALUES (5, '11111111-1111-1111-1111-111111111111', 'mount', 'por_cima', false, now());
                INSERT INTO training_sessions (id, user_id, date, attire, format, duration_minutes)
                    VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '2026-10-20', 'gi', 'aula', 90);
                INSERT INTO rolls (id, session_id, "order") VALUES (1, '22222222-2222-2222-2222-222222222222', 0),
                    (2, '22222222-2222-2222-2222-222222222222', 1);
                INSERT INTO roll_finishes (roll_id, "order", by_me, technique_id, node_id) VALUES
                    (1, 0, true, 'armbar', 5),
                    (2, 0, true, 'armbar', 5),
                    (2, 1, false, 'armbar', 5),
                    (2, 2, false, NULL, 5),
                    (2, 3, true, 'armbar', NULL);
                """);

            await migrator.MigrateAsync();

            var edges = await db.GameEdges.OrderBy(e => e.AgainstMe).ToListAsync();
            Assert.Equal(
                [(5L, (long?)null, EdgeKind.Finalizacao, "armbar", false), (5L, (long?)null, EdgeKind.Finalizacao, "armbar", true)],
                edges.Select(e => (e.FromNodeId, e.ToNodeId, e.Kind, e.TechniqueId, e.AgainstMe)));
            Assert.Equal(5, await db.RollFinishes.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
