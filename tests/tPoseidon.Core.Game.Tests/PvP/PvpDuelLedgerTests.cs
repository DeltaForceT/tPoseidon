using tPoseidon.Core.Data;
using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Game.Tests.PvP;

public sealed class PvpDuelLedgerTests
{
    private sealed class FakeStore : IPvpStore
    {
        public Dictionary<ulong, PvpLadder> Ladders { get; } = new();
        public List<PvpMatch> Matches { get; } = new();

        public PvpMatch RecordMatch(PvpMatch match)
        {
            Matches.Add(match);
            return match;
        }

        public IReadOnlyList<PvpMatch> GetMatchesForCharacter(ulong characterId, int limit = 50) => Matches.Where(match => match.CharacterId == characterId).ToList();

        public PvpLadder GetOrCreateLadder(ulong characterId) =>
            Ladders.TryGetValue(characterId, out var ladder) ? ladder : Ladders[characterId] = new PvpLadder { CharacterId = characterId };

        public PvpLadder UpsertLadder(PvpLadder ladder) => Ladders[ladder.CharacterId] = ladder;

        public bool DeleteLadder(ulong characterId) => Ladders.Remove(characterId);
    }

    private static PvpDuelOutcome Outcome(bool cancelled) => new(cancelled, null, new[]
    {
        new PvpDuelSeatOutcome(1, 9, false, true, 0, 5000, PvpRecord.Fresh, 15, 0, PvpDuelOutcome.CodeWin),
        new PvpDuelSeatOutcome(2, 9, false, false, 0, 5000, PvpRecord.Fresh, -15, 0, PvpDuelOutcome.CodeLose),
    });

    [Fact]
    public void AFinishedDuelUpdatesBothLaddersAndWritesTwoMatchRows()
    {
        var store = new FakeStore();
        var begin = new DateTimeOffset(2026, 10, 24, 20, 0, 0, TimeSpan.Zero);
        var end = begin.AddMinutes(3);
        PvpDuelLedger.Save(store, Outcome(cancelled: false), begin, end);
        Assert.Equal(1015, store.Ladders[1].Ladder);
        Assert.Equal((short)1, store.Ladders[1].WinCount);
        Assert.Equal((short)1, store.Ladders[1].MatchCount);
        Assert.Equal(985, store.Ladders[2].Ladder);
        Assert.Equal((short)1, store.Ladders[2].LoseCount);
        Assert.Equal(2, store.Matches.Count);
        var first = store.Matches[0];
        Assert.Equal(new byte[] { 0, 1 }, store.Matches.Select(match => match.JoinType));
        Assert.Equal(1UL, first.CharacterId);
        Assert.Equal(2UL, first.OpponentCharacterId);
        Assert.Equal((short)9, first.MatchMap);
        Assert.Equal(PvpDuel.TimeoutMinutes, first.MatchTimeout);
        Assert.Equal(PvpDuelLedger.ResultWin, first.MatchResult);
        Assert.Equal(PvpDuelLedger.ResultLose, store.Matches[1].MatchResult);
        Assert.Equal(-15, store.Matches[1].GetLadder);
        Assert.Equal(begin, first.MatchBegin);
        Assert.Equal(end, first.MatchEnd);
    }

    [Fact]
    public void ACancelledDuelWritesNothing()
    {
        var store = new FakeStore();
        PvpDuelLedger.Save(store, Outcome(cancelled: true), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Empty(store.Ladders);
        Assert.Empty(store.Matches);
    }

    [Fact]
    public void ADisconnectIsCountedOnTheLeaversLadder()
    {
        var store = new FakeStore();
        var outcome = new PvpDuelOutcome(false, null, new[]
        {
            new PvpDuelSeatOutcome(1, 9, false, true, 2, 100, PvpRecord.Fresh, 0, 0, PvpDuelOutcome.CodeWin),
            new PvpDuelSeatOutcome(2, 9, false, false, 2, 100, PvpRecord.Fresh, 0, 0, PvpDuelOutcome.CodeDisconnect),
        });
        PvpDuelLedger.Save(store, outcome, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        Assert.Equal((short)1, store.Ladders[2].DiscCount);
        Assert.Equal(1000, store.Ladders[2].Ladder);
        Assert.Equal(PvpDuelLedger.ResultDisconnect, store.Matches[1].MatchResult);
    }
}
