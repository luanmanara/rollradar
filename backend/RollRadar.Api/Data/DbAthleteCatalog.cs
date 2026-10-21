using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Athletes;

namespace RollRadar.Api.Data;

public static class DbAthleteCatalog
{
    /// <summary>The athletes imported with the collector's "atletas importar", by name.</summary>
    public static AthleteCatalog Load(RollRadarDbContext db) =>
        new(db.Athletes
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .AsEnumerable()
            .Select(a => new Athlete(a.Name, a.Aliases)));
}
