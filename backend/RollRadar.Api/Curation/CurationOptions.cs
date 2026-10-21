namespace RollRadar.Api.Curation;

public class CurationOptions
{
    public const string Section = "Curation";

    /// <summary>Key the curation page sends in <see cref="CuratorKeyFilter.Header"/>. Without it, curation is closed.</summary>
    public string? AdminKey { get; set; }

    /// <summary>
    /// Secret used to hash IP addresses. When missing, a random one is made at startup, so the same IP stops
    /// matching its earlier votes after a restart; set it once votes are stored in the database.
    /// </summary>
    public string? IpHashKey { get; set; }

    /// <summary>Votes accepted per IP address per minute.</summary>
    public int VotesPerMinute { get; set; } = 30;
}
