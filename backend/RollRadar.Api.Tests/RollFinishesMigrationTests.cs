using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RollRadar.Api.Data;

namespace RollRadar.Api.Tests;

/// <summary>
/// The migration that moved a roll's single result into roll_finishes keeps every submission already recorded.
/// Runs on a database of its own in the same container, migrated step by step.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RollFinishesMigrationTests(PostgresFixture database)
{
    [Fact]
    public async Task Rolls_recorded_with_a_result_become_finishes()
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
            await migrator.MigrateAsync(db.Database.GetMigrations().Single(m => m.EndsWith("_AddTrainingDiary")));
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO users (id, email) VALUES ('11111111-1111-1111-1111-111111111111', 'ana@exemplo.com');
                INSERT INTO techniques (id, english, portuguese, category) VALUES ('armbar', 'Armbar', 'Chave de braço', 'Chaves de braço');
                INSERT INTO training_sessions (id, user_id, date, attire, format, duration_minutes)
                    VALUES ('22222222-2222-2222-2222-222222222222', '11111111-1111-1111-1111-111111111111', '2026-10-20', 'gi', 'aula', 90);
                INSERT INTO rolls (session_id, "order", result, finish_technique_id, notes) VALUES
                    ('22222222-2222-2222-2222-222222222222', 0, 'finalizei', 'armbar', 'bom'),
                    ('22222222-2222-2222-2222-222222222222', 1, 'fui_finalizado', NULL, NULL),
                    ('22222222-2222-2222-2222-222222222222', 2, 'nenhum', NULL, NULL);
                """);

            await migrator.MigrateAsync();

            var finishes = await db.RollFinishes.OrderBy(f => f.Roll!.Order)
                .Select(f => new { f.Roll!.Order, f.ByMe, f.TechniqueId, FinishOrder = f.Order, f.NodeId })
                .ToListAsync();
            Assert.Equal(
                [(0, true, "armbar", 0), (1, false, null, 0)],
                finishes.Select(f => (f.Order, f.ByMe, f.TechniqueId, f.FinishOrder)));
            Assert.All(finishes, f => Assert.Null(f.NodeId));
            Assert.Equal(3, await db.Rolls.CountAsync());
            Assert.Equal("bom", (await db.Rolls.SingleAsync(r => r.Order == 0)).Notes);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
