using Microsoft.EntityFrameworkCore;
using RollRadar.Api.Curation;
using RollRadar.Api.Data;
using DbCurationStatus = RollRadar.Api.Data.CurationStatus;

namespace RollRadar.Api.Tests;

/// <summary>Same behavior as <see cref="InMemoryCurationStore"/> (see CurationTests), stored in PostgreSQL.</summary>
[Collection(PostgresCollection.Name)]
public sealed class EfCurationStoreTests(PostgresFixture database) : IAsyncLifetime
{
    private const string MomentId = "a1_b-C.armbar.66";
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static MomentVote Vote(string voter, string ip, bool useful, int minute = 0) =>
        new(MomentId, voter, ip, useful, T0.AddMinutes(minute));

    public async Task InitializeAsync()
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        var channel = new ChannelEntity { Name = "IBJJF" };
        db.Techniques.Add(new TechniqueEntity { Id = "armbar", English = "Armbar", Portuguese = "Chave de braço", Category = "Chaves de braço" });
        db.Videos.Add(new VideoEntity
        {
            Id = "a1_b-C", Title = "A vs B", Channel = channel,
            Moments = [new TechniqueMomentEntity { VideoId = "a1_b-C", TechniqueId = "armbar", StartSeconds = 66, Term = "armbar", Snippet = "" }],
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>A new context per call, like one request each in the API.</summary>
    private T With<T>(Func<EfCurationStore, T> action)
    {
        using var db = database.CreateContext();
        return action(new EfCurationStore(db));
    }

    private void With(Action<EfCurationStore> action) => With(store => { action(store); return 0; });

    [Fact]
    public void Voting_again_replaces_the_earlier_vote()
    {
        With(s => s.SaveVote(Vote("v1", "ip1", true)));
        With(s => s.SaveVote(Vote("v1", "ip1", false, 1)));

        Assert.Equal(new VoteTally(0, 1), With(s => s.GetTally(MomentId)));
        var vote = With(s => s.GetVote(MomentId, "v1"))!;
        Assert.False(vote.Useful);
        Assert.Equal(T0.AddMinutes(1), vote.VotedAt);

        using var db = database.CreateContext();
        Assert.Equal(1, db.MomentVotes.Count());
    }

    [Fact]
    public void Each_IP_counts_once_with_its_latest_vote()
    {
        // Same network, cookie cleared between votes.
        With(s => s.SaveVote(Vote("v1", "ip1", false)));
        With(s => s.SaveVote(Vote("v2", "ip1", false, 1)));
        With(s => s.SaveVote(Vote("v3", "ip1", true, 2)));
        With(s => s.SaveVote(Vote("v4", "ip2", true)));

        Assert.Equal(new VoteTally(2, 0), With(s => s.GetTally(MomentId)));
    }

    [Fact]
    public void Removed_vote_stops_counting()
    {
        With(s => s.SaveVote(Vote("v1", "ip1", true)));
        With(s => s.RemoveVote(MomentId, "v1"));

        Assert.Equal(VoteTally.Empty, With(s => s.GetTally(MomentId)));
        Assert.Null(With(s => s.GetVote(MomentId, "v1")));
    }

    [Fact]
    public void Status_is_stored_on_the_moment()
    {
        Assert.Equal(Curation.CurationStatus.Pending, With(s => s.GetStatus(MomentId)));

        With(s => s.SetStatus(MomentId, Curation.CurationStatus.Bad));
        Assert.Equal(Curation.CurationStatus.Bad, With(s => s.GetStatus(MomentId)));
        using (var db = database.CreateContext())
        {
            var moment = db.TechniqueMoments.AsNoTracking().Single();
            Assert.Equal(DbCurationStatus.Ruim, moment.CurationStatus);
            Assert.NotNull(moment.CuratedAt);
        }

        With(s => s.SetStatus(MomentId, Curation.CurationStatus.Pending));
        Assert.Equal(Curation.CurationStatus.Pending, With(s => s.GetStatus(MomentId)));
        using (var db = database.CreateContext())
            Assert.Null(db.TechniqueMoments.AsNoTracking().Single().CuratedAt);
    }

    [Fact]
    public void Unknown_moments_are_ignored()
    {
        const string unknown = "a1_b-C.armbar.67";
        With(s => s.SaveVote(Vote("v1", "ip1", true) with { MomentId = unknown }));
        With(s => s.SetStatus(unknown, Curation.CurationStatus.Good));

        Assert.Equal(VoteTally.Empty, With(s => s.GetTally(unknown)));
        Assert.Equal(Curation.CurationStatus.Pending, With(s => s.GetStatus(unknown)));
        Assert.Null(With(s => s.GetVote("garbage", "v1")));
        using var db = database.CreateContext();
        Assert.Equal(0, db.MomentVotes.Count());
    }
}
