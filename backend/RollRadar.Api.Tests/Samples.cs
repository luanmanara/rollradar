using RollRadar.Api.Techniques;

namespace RollRadar.Api.Tests;

/// <summary>
/// The real technique catalog, as exported from the database (catalogo exportar), kept here so the tests do not need a database
/// or the samples folder.
/// </summary>
public static class Samples
{
    public static string CatalogPath { get; } = Path.Combine(FindRepositoryRoot(), "backend", "RollRadar.Api.Tests", "Data", "tecnicas-bjj.csv");

    public static IReadOnlyList<Technique> ReadCatalog()
    {
        using var reader = new StreamReader(CatalogPath);
        return TechniqueCatalogReader.Read(reader);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RollRadar.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("RollRadar.sln not found");
    }
}
