using RollRadar.Api.Curation;
using RollRadar.Api.Models;
using RollRadar.Api.Services;
using RollRadar.Api.Techniques;

namespace RollRadar.Api.Tests;

public class CurationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private static MomentVote Vote(string voter, string ip, bool useful, int minute = 0) =>
        new("a.armbar.66", voter, ip, useful, T0.AddMinutes(minute));

    [Fact]
    public void Voting_again_replaces_the_earlier_vote()
    {
        var store = new InMemoryCurationStore();
        store.SaveVote(Vote("v1", "ip1", true));
        store.SaveVote(Vote("v1", "ip1", false, 1));

        Assert.Equal(new VoteTally(0, 1), store.GetTally("a.armbar.66"));
        Assert.False(store.GetVote("a.armbar.66", "v1")!.Useful);
    }

    [Fact]
    public void Each_IP_counts_once_with_its_latest_vote()
    {
        var store = new InMemoryCurationStore();
        // Same network, cookie cleared between votes.
        store.SaveVote(Vote("v1", "ip1", false));
        store.SaveVote(Vote("v2", "ip1", false, 1));
        store.SaveVote(Vote("v3", "ip1", true, 2));
        store.SaveVote(Vote("v4", "ip2", true));

        Assert.Equal(new VoteTally(2, 0), store.GetTally("a.armbar.66"));
    }

    [Fact]
    public void Removed_vote_stops_counting()
    {
        var store = new InMemoryCurationStore();
        store.SaveVote(Vote("v1", "ip1", true));
        store.RemoveVote("a.armbar.66", "v1");

        Assert.Equal(VoteTally.Empty, store.GetTally("a.armbar.66"));
        Assert.Null(store.GetVote("a.armbar.66", "v1"));
    }

    [Fact]
    public void Status_is_pending_until_the_curator_decides()
    {
        var store = new InMemoryCurationStore();
        Assert.Equal(CurationStatus.Pending, store.GetStatus("a.armbar.66"));

        store.SetStatus("a.armbar.66", CurationStatus.Bad);
        Assert.Equal(CurationStatus.Bad, store.GetStatus("a.armbar.66"));

        store.SetStatus("a.armbar.66", CurationStatus.Pending);
        Assert.Equal(CurationStatus.Pending, store.GetStatus("a.armbar.66"));
    }

    [Fact]
    public void Moments_get_a_stable_id_from_video_technique_and_second()
    {
        var catalog = new TechniqueCatalog([new("Armbar", "Chave de braço", "Chaves de braço", ["Armbar"], [])]);
        var service = new VideoSearchService(
            [new FightVideo("a_1-x", "A vs B", "", "", "IBJJF", null,
                [new("Armlock", 66, "", "armbar"), new("Kimura", 90, "")])],
            catalog);

        var ids = service.Search("", null).Videos.Single().Moments.Select(m => m.Id);

        Assert.Equal(["a_1-x.armbar.66", "a_1-x.kimura.90"], ids);
        Assert.True(service.MomentExists("a_1-x.armbar.66"));
        Assert.False(service.MomentExists("a_1-x.armbar.67"));
    }
}
