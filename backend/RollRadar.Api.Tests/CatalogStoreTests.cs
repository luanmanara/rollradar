using RollRadar.Api.Techniques;
using RollRadar.Coletor;

namespace RollRadar.Api.Tests;

[Collection(PostgresCollection.Name)]
public sealed class CatalogStoreTests(PostgresFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static string Csv(IEnumerable<Technique> techniques)
    {
        using var writer = new StringWriter();
        TechniqueCatalogWriter.Write(techniques, writer);
        return writer.ToString();
    }

    [Fact]
    public async Task The_real_catalog_comes_back_the_same_from_the_database_and_from_its_export()
    {
        var catalog = Samples.ReadCatalog();
        var store = new CatalogStore(database.CreateContext, new ImportStats());

        await store.ImportAsync(catalog);
        var loaded = store.Load();

        Assert.Equal(Csv(catalog), Csv(loaded));
        Assert.Equal(catalog.Select(t => t.Id), TechniqueCatalogReader.Read(new StringReader(Csv(loaded))).Select(t => t.Id));
    }

    [Fact]
    public async Task Importing_an_edited_csv_updates_the_technique_and_keeps_the_ones_left_out()
    {
        var stats = new ImportStats();
        var store = new CatalogStore(database.CreateContext, stats);
        await store.ImportAsync([
            new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock"]),
            new("Omoplata", "Omoplata", "Chaves de braço", ["Omoplata"], []),
        ]);

        await store.ImportAsync([new("Armbar", "Chave de braço", "Finalizações", ["Armbar"], ["Armlock", "Juji-gatame"])]);

        var armbar = store.Load().Single(t => t.Id == "armbar");
        Assert.Equal("Finalizações", armbar.Category);
        Assert.Equal(["Armlock", "Juji-gatame"], armbar.Aliases);
        Assert.Contains(store.Load(), t => t.Id == "omoplata");
        Assert.Contains(stats.Warnings, w => w.Contains("omoplata"));
    }
}
