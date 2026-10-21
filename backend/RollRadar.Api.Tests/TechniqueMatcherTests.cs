using RollRadar.Api.Techniques;

namespace RollRadar.Api.Tests;

/// <summary>Same rules as montar_tecnicas.py (checked against its output on 45 real fights: identical hits).</summary>
public class TechniqueMatcherTests
{
    private static readonly Technique[] Techniques =
    [
        new("Heel Hook", "Chave de calcanhar", "Chaves de perna", ["Heel Hook"], []),
        new("Inside Heel Hook", "Heel hook por dentro", "Chaves de perna", ["Inside Heel Hook"], []),
        new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], ["Armlock", "juji-gatame", "Braço esticado"]),
        new("Fireman's Carry", "Carregada do bombeiro", "Quedas", ["Fireman's Carry"], []),
        new("De La Riva Guard", "Guarda De La Riva", "Guardas", ["De La Riva Guard"], ["DLR"]),
        new("Sweep", "Raspagem", "Fundamentos", ["Sweep"], []),
        new("50/50", "50/50", "Guardas", ["50/50"], []),
    ];

    private readonly TechniqueMatcher _matcher = new(Techniques);

    private static List<CaptionLine> Lines(params (double Start, string Text)[] lines) =>
        lines.Select(l => new CaptionLine(l.Start, l.Text)).ToList();

    [Fact]
    public void Prefers_the_longest_term_of_a_technique_and_keeps_the_term_found()
    {
        var hit = Assert.Single(_matcher.Find(Lines((10, "he plays the De La Riva guard now"))));

        Assert.Equal("de-la-riva-guard", hit.TechniqueId);
        Assert.Equal("de la riva guard", hit.Term);
    }

    [Fact]
    public void Each_technique_is_searched_on_its_own()
    {
        // As in montar_tecnicas.py: "inside heel hook" is also a mention of "heel hook".
        var hits = _matcher.Find(Lines((10, "he goes for the INSIDE heel-hook now")));

        Assert.Equal([("heel-hook", "heel hook"), ("inside-heel-hook", "inside heel hook")],
            hits.Select(h => (h.TechniqueId, h.Term)).OrderBy(h => h.TechniqueId));
    }

    [Fact]
    public void Finds_a_name_split_between_two_lines_on_the_line_where_it_starts()
    {
        var hits = _matcher.Find(Lines((10, "and now the heel"), (12.5, "hook is tight"), (14, "wow")));

        var hit = Assert.Single(hits);
        Assert.Equal(10, hit.Start);
        Assert.Equal("and now the heel hook is tight", hit.Text);
    }

    [Fact]
    public void Gives_the_previous_and_next_line_as_context()
    {
        var hits = _matcher.Find(Lines((1, "first"), (2, "  an armbar  "), (3, "last"), (4, "unrelated")));

        Assert.Equal("first an armbar last", Assert.Single(hits).Text);
    }

    [Fact]
    public void Skips_mentions_closer_than_30_seconds_to_the_previous_hit()
    {
        var hits = _matcher.Find(Lines((0, "armbar"), (29.9, "armbar again"), (35, "and armlock")));

        Assert.Equal([0, 35], hits.Select(h => h.Start));
        Assert.Equal(["armbar", "armlock"], hits.Select(h => h.Term));
    }

    [Fact]
    public void Uses_only_the_whitelisted_aliases_and_the_extra_short_forms()
    {
        var terms = _matcher.TermsById;

        Assert.Contains("juji gatame", terms["armbar"]);
        Assert.DoesNotContain("braco esticado", terms["armbar"]);
        Assert.Contains("de la riva", terms["de-la-riva-guard"]);
        Assert.Contains("dlr", terms["de-la-riva-guard"]);
    }

    [Fact]
    public void Skips_generic_techniques_and_digit_only_terms()
    {
        Assert.False(_matcher.TermsById.ContainsKey("sweep"));
        Assert.Empty(_matcher.Find(Lines((1, "nice sweep"))));
        Assert.DoesNotContain(_matcher.TermsById.Values.SelectMany(t => t), t => t.All(char.IsDigit));
    }

    [Fact]
    public void Ignores_apostrophes_accents_and_partial_words()
    {
        Assert.Equal("fireman-s-carry", Assert.Single(_matcher.Find(Lines((1, "a firemans carry")))).TechniqueId);
        Assert.Single(_matcher.Find(Lines((1, "Chave de braço!"))));
        Assert.Empty(_matcher.Find(Lines((1, "armbars everywhere"), (40, "the dlrx"))));
    }

    [Fact]
    public void Moments_are_rounded_down_and_merged_like_the_api()
    {
        var moments = _matcher.FindMoments(Lines((61.9, "an armbar"), (80, "armlock again"), (100.2, "the armbar")));

        Assert.Equal([61, 100], moments.Select(m => m.StartSeconds));
    }
}
