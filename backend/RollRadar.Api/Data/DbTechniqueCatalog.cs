using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Data;

public static class DbTechniqueCatalog
{
    /// <summary>The catalog as imported from tecnicas-bjj.csv, in CSV order, so the API needs no files to run.</summary>
    public static TechniqueCatalog Load(RollRadarDbContext db)
    {
        var techniques = db.Techniques
            .AsNoTracking()
            .Include(t => t.Aliases)
            .OrderBy(t => t.Position)
            .AsEnumerable()
            .Select(t =>
            {
                var aliases = t.Aliases.OrderBy(a => a.Position).ToList();
                return new Technique(
                    t.English,
                    t.Portuguese,
                    t.Category,
                    aliases.Where(a => a.Kind == AliasKind.EnglishTerm).Select(a => a.Alias).ToList(),
                    aliases.Where(a => a.Kind == AliasKind.Alias).Select(a => a.Alias).ToList());
            });
        return new TechniqueCatalog(techniques);
    }
}
