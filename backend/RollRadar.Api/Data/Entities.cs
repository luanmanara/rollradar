namespace RollRadar.Api.Data;

/// <summary>A YouTube channel the fights come from (FloGrappling, IBJJF).</summary>
public class ChannelEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? YoutubeChannelId { get; set; }

    /// <summary>Channel URL as given to the collector (e.g. https://www.youtube.com/@FloGrappling); its order there is the priority.</summary>
    public string? Url { get; set; }

    public List<VideoEntity> Videos { get; set; } = [];
}

/// <summary>Status of a fight in the caption queue (same values as cache/fila.json of the Python scripts).</summary>
public enum CaptionStatus
{
    Pendente,
    Legenda,
    SemLegenda,
    Curta,
    PuladaEvento,
}

/// <summary>A fight video; the id is the YouTube video id.</summary>
public class VideoEntity
{
    public required string Id { get; set; }
    public int ChannelId { get; set; }
    public required string Title { get; set; }
    public DateOnly? UploadDate { get; set; }
    public int? DurationSeconds { get; set; }
    public long? ViewCount { get; set; }
    public string? Language { get; set; }
    public CaptionStatus CaptionStatus { get; set; } = CaptionStatus.Pendente;

    /// <summary>Why the fight has no captions (e.g. "TranscriptsDisabled"); null when it has or the reason is unknown.</summary>
    public string? CaptionError { get; set; }

    /// <summary>When the collector last got an answer from YouTube for this fight's captions.</summary>
    public DateTime? CaptionCheckedAt { get; set; }

    /// <summary>Event from the end of the title ("A vs B / Manaus Open 2026" -> "Manaus Open 2026"), to skip events without captions.</summary>
    public string? Event { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ChannelEntity? Channel { get; set; }
    public List<TranscriptSegmentEntity> Segments { get; set; } = [];
    public List<TechniqueMomentEntity> Moments { get; set; } = [];
}

/// <summary>One caption line of a video.</summary>
public class TranscriptSegmentEntity
{
    public long Id { get; set; }
    public required string VideoId { get; set; }
    public double StartSeconds { get; set; }
    public double? DurationSeconds { get; set; }
    public required string Text { get; set; }

    public VideoEntity? Video { get; set; }
}

/// <summary>A catalog technique; the id is the slug from <see cref="Techniques.Technique.ToId"/>.</summary>
public class TechniqueEntity
{
    public required string Id { get; set; }
    public required string English { get; set; }
    public required string Portuguese { get; set; }
    public required string Category { get; set; }

    /// <summary>Row order in tecnicas-bjj.csv, which is the order the API lists techniques in.</summary>
    public int Position { get; set; }

    public List<TechniqueAliasEntity> Aliases { get; set; } = [];
}

public enum AliasKind
{
    EnglishTerm,
    Alias,
}

/// <summary>A name a technique can be searched by: one of its English terms or an alias.</summary>
public class TechniqueAliasEntity
{
    public int Id { get; set; }
    public required string TechniqueId { get; set; }
    public required string Alias { get; set; }

    /// <summary>Alias after <see cref="Common.TextNormalizer.Normalize"/>.</summary>
    public required string NormalizedAlias { get; set; }

    public AliasKind Kind { get; set; }

    /// <summary>Order within the technique's English terms or aliases, as written in the CSV.</summary>
    public int Position { get; set; }

    public TechniqueEntity? Technique { get; set; }
}

/// <summary>An athlete reviewed by hand (collector's "atletas" CSV); fights are found by name or alias in the title.</summary>
public class AthleteEntity
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Name after <see cref="Common.TextNormalizer.Normalize"/>, unique.</summary>
    public required string NormalizedName { get; set; }

    /// <summary>Other spellings in the titles, e.g. "Mica Galvao" for "Mica Galvão".</summary>
    public List<string> Aliases { get; set; } = [];
}

/// <summary>Manual curation of a moment (good/bad).</summary>
public enum CurationStatus
{
    Pendente,
    Bom,
    Ruim,
}

/// <summary>A moment in a video where a technique is mentioned in the captions.</summary>
public class TechniqueMomentEntity
{
    public long Id { get; set; }
    public required string VideoId { get; set; }
    public required string TechniqueId { get; set; }
    public int StartSeconds { get; set; }

    /// <summary>Term found in the captions, e.g. "armlock".</summary>
    public required string Term { get; set; }

    public required string Snippet { get; set; }
    public CurationStatus CurationStatus { get; set; } = CurationStatus.Pendente;
    public DateTime? CuratedAt { get; set; }

    public VideoEntity? Video { get; set; }
    public TechniqueEntity? Technique { get; set; }
    public List<MomentVoteEntity> Votes { get; set; } = [];
}

/// <summary>A visitor's "useful / not useful" vote on a moment.</summary>
public class MomentVoteEntity
{
    public long Id { get; set; }
    public long MomentId { get; set; }

    /// <summary>Random id from the signed rr_voter cookie.</summary>
    public required string VoterId { get; set; }

    /// <summary>HMAC-SHA256 of the voter IP (hex, 32 chars); the raw IP is never stored.</summary>
    public required string IpHash { get; set; }

    public bool Useful { get; set; }
    public DateTime VotedAt { get; set; }

    public TechniqueMomentEntity? Moment { get; set; }
}

/// <summary>A bearer token of an external client of the data API (see <see cref="Auth.ApiTokens"/>).</summary>
public class ApiTokenEntity
{
    public int Id { get; set; }

    /// <summary>Who the token was given to, e.g. "parceiro-x"; used to list and revoke it.</summary>
    public required string Name { get; set; }

    /// <summary>SHA-256 of the token (hex, 64 chars); the token itself is never stored.</summary>
    public required string TokenHash { get; set; }

    /// <summary>First characters of the token ("rr_AbCdE"), to recognize it in a list.</summary>
    public required string TokenStart { get; set; }

    /// <summary>What the token allows, from <see cref="Auth.ApiScopes"/>.</summary>
    public List<string> Scopes { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    /// <summary>Null when the token does not expire.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
