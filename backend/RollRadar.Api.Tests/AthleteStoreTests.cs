using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Athletes;
using RollRadar.Api.Data;
using RollRadar.Coletor;

namespace RollRadar.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class AthleteStoreTests(PostgresFixture database) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        var channel = new ChannelEntity { Name = "FloGrappling", YoutubeChannelId = "UCflo" };
        db.Techniques.Add(new TechniqueEntity { Id = "armbar", English = "Armbar", Portuguese = "Chave de braço", Category = "Chaves de braço" });
        db.Videos.AddRange(
            Fight("v1", "Full Match: Mica Galvao vs Tye Ruotolo | WNO"),
            Fight("v2", "Mica Galvão vs Kaynan Duarte"),
            Fight("v3", "Recapping WNO 28: Mica Galvão vs Gordon Ryan"));
        await db.SaveChangesAsync();

        VideoEntity Fight(string id, string title) => new()
        {
            Id = id, Title = title, Channel = channel, CaptionStatus = CaptionStatus.Legenda,
            Moments = [new TechniqueMomentEntity { VideoId = id, TechniqueId = "armbar", StartSeconds = 10, Term = "armbar", Snippet = "armbar" }],
        };
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Exporting_an_empty_catalog_suggests_the_names_of_the_titles()
    {
        var rows = new AthleteStore(database.CreateContext, new ImportStats()).Export();

        Assert.Equal(("Mica Galvão", 3, true), (rows[0].Athlete.Name, rows[0].Fights, rows[0].Suggested));
        Assert.Equal(["Mica Galvão", "Gordon Ryan", "Kaynan Duarte", "Tye Ruotolo"], rows.Select(r => r.Athlete.Name));
    }

    [Fact]
    public async Task Importing_makes_the_catalog_the_csv_and_exporting_lists_it_before_the_new_names()
    {
        var stats = new ImportStats();
        var store = new AthleteStore(database.CreateContext, stats);
        await store.ImportAsync([new Athlete("Gordon Ryan", []), new Athlete("Lixo", [])]);

        await store.ImportAsync([
            new Athlete("Mica Galvão", ["Mica Galvao"]),
            new Athlete("Tye Ruotolo", ["mica galvao"]),
            new Athlete("Gordon Ryan", []),
        ]);
        var rows = store.Export();

        await using var db = database.CreateContext();
        Assert.Equal(["Gordon Ryan", "Mica Galvão", "Tye Ruotolo"], await db.Athletes.OrderBy(a => a.Name).Select(a => a.Name).ToListAsync());
        Assert.Equal(1, stats["athletes"].Deleted);
        Assert.Contains(stats.Warnings, w => w.Contains("mica galvao"));
        Assert.Equal(
            [("Mica Galvão", 3, false), ("Gordon Ryan", 1, false), ("Tye Ruotolo", 1, false), ("Kaynan Duarte", 1, true)],
            rows.Select(r => (r.Athlete.Name, r.Fights, r.Suggested)));
    }
}
