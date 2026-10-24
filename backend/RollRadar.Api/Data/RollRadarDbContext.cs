using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RollRadar.Api.Data;

/// <summary>
/// PostgreSQL schema. Table and column names are snake_case (UseSnakeCaseNamingConvention in
/// Program.cs); enums are stored as snake_case strings, e.g. CaptionStatus.SemLegenda -> "sem_legenda".
/// Also keeps the Data Protection keys, so login and voter cookies stay valid after a restart or deploy.
/// </summary>
public class RollRadarDbContext(DbContextOptions<RollRadarDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<ChannelEntity> Channels => Set<ChannelEntity>();
    public DbSet<VideoEntity> Videos => Set<VideoEntity>();
    public DbSet<TranscriptSegmentEntity> TranscriptSegments => Set<TranscriptSegmentEntity>();
    public DbSet<TechniqueEntity> Techniques => Set<TechniqueEntity>();
    public DbSet<TechniqueAliasEntity> TechniqueAliases => Set<TechniqueAliasEntity>();
    public DbSet<TechniqueMomentEntity> TechniqueMoments => Set<TechniqueMomentEntity>();
    public DbSet<MomentVoteEntity> MomentVotes => Set<MomentVoteEntity>();
    public DbSet<ApiTokenEntity> ApiTokens => Set<ApiTokenEntity>();
    public DbSet<AthleteEntity> Athletes => Set<AthleteEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<UserLoginEntity> UserLogins => Set<UserLoginEntity>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<TrainingSessionEntity> TrainingSessions => Set<TrainingSessionEntity>();
    public DbSet<SessionTechniqueEntity> SessionTechniques => Set<SessionTechniqueEntity>();
    public DbSet<PartnerEntity> Partners => Set<PartnerEntity>();
    public DbSet<RollEntity> Rolls => Set<RollEntity>();
    public DbSet<GameNodeEntity> GameNodes => Set<GameNodeEntity>();
    public DbSet<RollStepEntity> RollSteps => Set<RollStepEntity>();
    public DbSet<RollFinishEntity> RollFinishes => Set<RollFinishEntity>();
    public DbSet<FocusPointEntity> FocusPoints => Set<FocusPointEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChannelEntity>(e =>
        {
            e.ToTable("channels");
            e.HasIndex(c => c.YoutubeChannelId).IsUnique();
        });

        modelBuilder.Entity<VideoEntity>(e =>
        {
            e.ToTable("videos");
            e.Property(v => v.CaptionStatus)
                .HasConversion(new SnakeCaseEnumConverter<CaptionStatus>())
                .HasDefaultValue(CaptionStatus.Pendente);
            e.Property(v => v.CreatedAt).HasDefaultValueSql("now()");
            e.Property(v => v.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(v => v.Channel).WithMany(c => c.Videos).HasForeignKey(v => v.ChannelId);
            e.HasIndex(v => new { v.ChannelId, v.UploadDate });
            e.HasIndex(v => v.CaptionStatus);
        });

        modelBuilder.Entity<TranscriptSegmentEntity>(e =>
        {
            e.ToTable("transcript_segments");
            e.HasOne(s => s.Video).WithMany(v => v.Segments).HasForeignKey(s => s.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.VideoId, s.StartSeconds });
        });

        modelBuilder.Entity<TechniqueEntity>(e => e.ToTable("techniques"));

        modelBuilder.Entity<TechniqueAliasEntity>(e =>
        {
            e.ToTable("technique_aliases");
            e.Property(a => a.Kind).HasConversion(new SnakeCaseEnumConverter<AliasKind>());
            e.HasOne(a => a.Technique).WithMany(t => t.Aliases).HasForeignKey(a => a.TechniqueId)
                .OnDelete(DeleteBehavior.Cascade);
            // Per kind: an alias may repeat an English term ("Z-guard" for "Z-Guard / Knee Shield").
            e.HasIndex(a => new { a.TechniqueId, a.Kind, a.NormalizedAlias }).IsUnique();
            e.HasIndex(a => a.NormalizedAlias);
        });

        modelBuilder.Entity<TechniqueMomentEntity>(e =>
        {
            e.ToTable("technique_moments");
            e.Property(m => m.CurationStatus)
                .HasConversion(new SnakeCaseEnumConverter<CurationStatus>())
                .HasDefaultValue(CurationStatus.Pendente);
            e.HasOne(m => m.Video).WithMany(v => v.Moments).HasForeignKey(m => m.VideoId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Technique).WithMany().HasForeignKey(m => m.TechniqueId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(m => new { m.VideoId, m.TechniqueId, m.StartSeconds }).IsUnique();
            e.HasIndex(m => m.TechniqueId);
        });

        modelBuilder.Entity<MomentVoteEntity>(e =>
        {
            e.ToTable("moment_votes");
            e.Property(v => v.VotedAt).HasDefaultValueSql("now()");
            e.HasOne(v => v.Moment).WithMany(m => m.Votes).HasForeignKey(v => v.MomentId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(v => new { v.MomentId, v.VoterId }).IsUnique();
            e.HasIndex(v => new { v.MomentId, v.IpHash });
        });

        modelBuilder.Entity<AthleteEntity>(e =>
        {
            e.ToTable("athletes");
            e.HasIndex(a => a.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<ApiTokenEntity>(e =>
        {
            e.ToTable("api_tokens");
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.Name).IsUnique();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            e.Property(u => u.LastLoginAt).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<UserLoginEntity>(e =>
        {
            e.ToTable("user_logins");
            e.HasOne(l => l.User).WithMany(u => u.Logins).HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
        });

        modelBuilder.Entity<DataProtectionKey>(e => e.ToTable("data_protection_keys"));

        // Training diary: everything hangs from the user, so deleting the account deletes it all.
        modelBuilder.Entity<TrainingSessionEntity>(e =>
        {
            e.ToTable("training_sessions");
            e.Property(t => t.Id).ValueGeneratedNever();
            e.Property(t => t.Attire).HasConversion(new SnakeCaseEnumConverter<Attire>());
            e.Property(t => t.Format).HasConversion(new SnakeCaseEnumConverter<TrainingFormat>());
            e.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
            e.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.UserId, t.Date });
        });

        modelBuilder.Entity<SessionTechniqueEntity>(e =>
        {
            e.ToTable("session_techniques");
            e.HasKey(t => new { t.SessionId, t.TechniqueId });
            e.HasOne(t => t.Session).WithMany(s => s.Techniques).HasForeignKey(t => t.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Technique).WithMany().HasForeignKey(t => t.TechniqueId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PartnerEntity>(e =>
        {
            e.ToTable("partners");
            e.Property(p => p.Belt).HasConversion(new SnakeCaseEnumConverter<Belt>());
            e.Property(p => p.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.UserId, p.NormalizedName }).IsUnique();
        });

        modelBuilder.Entity<RollEntity>(e =>
        {
            e.ToTable("rolls");
            e.Property(r => r.PartnerBelt).HasConversion(new SnakeCaseEnumConverter<Belt>());
            e.HasOne(r => r.Session).WithMany(s => s.Rolls).HasForeignKey(r => r.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Partner).WithMany().HasForeignKey(r => r.PartnerId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => new { r.SessionId, r.Order });
        });

        modelBuilder.Entity<GameNodeEntity>(e =>
        {
            e.ToTable("game_nodes");
            e.Property(n => n.Side).HasConversion(new SnakeCaseEnumConverter<NodeSide>());
            e.Property(n => n.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.Technique).WithMany().HasForeignKey(n => n.TechniqueId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(n => new { n.UserId, n.TechniqueId, n.Side }).IsUnique();
        });

        modelBuilder.Entity<RollStepEntity>(e =>
        {
            e.ToTable("roll_steps");
            e.Property(s => s.Outcome).HasConversion(new SnakeCaseEnumConverter<StepOutcome>());
            e.HasOne(s => s.Roll).WithMany(r => r.Steps).HasForeignKey(s => s.RollId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Node).WithMany().HasForeignKey(s => s.NodeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.RollId, s.Order });
        });

        modelBuilder.Entity<RollFinishEntity>(e =>
        {
            e.ToTable("roll_finishes");
            e.HasOne(f => f.Roll).WithMany(r => r.Finishes).HasForeignKey(f => f.RollId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Technique).WithMany().HasForeignKey(f => f.TechniqueId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(f => f.Node).WithMany().HasForeignKey(f => f.NodeId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(f => new { f.RollId, f.Order });
        });

        modelBuilder.Entity<FocusPointEntity>(e =>
        {
            e.ToTable("focus_points");
            e.Property(f => f.Review).HasConversion(new SnakeCaseEnumConverter<FocusReview>());
            e.Property(f => f.CreatedAt).HasDefaultValueSql("now()");
            e.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.OriginSession).WithMany().HasForeignKey(f => f.OriginSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.ReviewedSession).WithMany().HasForeignKey(f => f.ReviewedSessionId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(f => new { f.UserId, f.Review, f.CreatedAt });
            // One point per session: editing the session edits its point.
            e.HasIndex(f => f.OriginSessionId).IsUnique();
        });
    }
}

/// <summary>The snake_case names of an enum, as stored and as the diary API sends them: NoGi <-> "no_gi".</summary>
public static class SnakeCaseEnum
{
    public static string Name<TEnum>(TEnum value) where TEnum : struct, Enum => SnakeCase.From(value.ToString());

    public static string? Name<TEnum>(TEnum? value) where TEnum : struct, Enum => value is { } v ? Name(v) : null;

    /// <summary>False for null, empty or unknown names.</summary>
    public static bool TryParse<TEnum>(string? name, out TEnum value) where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (Name(candidate) != name) continue;
            value = candidate;
            return true;
        }
        value = default;
        return false;
    }
}

/// <summary>Stores an enum as its snake_case name: SemLegenda <-> "sem_legenda".</summary>
public class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(ToStore, FromStore)
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => SnakeCase.From(v.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Names.ToDictionary(p => p.Value, p => p.Key);

    private static readonly Expression<Func<TEnum, string>> ToStore = v => Names[v];
    private static readonly Expression<Func<string, TEnum>> FromStore = s => Values[s];
}

public static partial class SnakeCase
{
    public static string From(string pascalCase) =>
        UpperLetterRegex().Replace(pascalCase, "_$1").ToLowerInvariant();

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex UpperLetterRegex();
}
