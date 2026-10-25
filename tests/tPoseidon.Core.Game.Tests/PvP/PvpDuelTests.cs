using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Game.Tests.PvP;

public sealed class PvpDuelTests
{
    private const ulong Challenger = 11;
    private const ulong Defender = 22;

    private static PvpDuel Duel(int challengerLadder = 1000, int defenderLadder = 1000) => new(
        Challenger, 7, new PvpRecord(challengerLadder, 4, 2, 2, 0),
        Defender, 7, new PvpRecord(defenderLadder, 6, 3, 2, 1));

    private static PvpDuel Fighting(int challengerLadder = 1000, int defenderLadder = 1000)
    {
        var duel = Duel(challengerLadder, defenderLadder);
        Assert.Single(duel.Tick(PvpDuel.PrepareMs));
        Assert.Equal(PvpDuelPhase.Battle, duel.Phase);
        return duel;
    }

    private static PvpDuelOutcome Finish(IReadOnlyList<PvpDuelEvent> events) => Assert.IsType<PvpDuelFinished>(Assert.Single(events)).Outcome;

    [Fact]
    public void PrepareLastsFiveSecondsThenTheBattleBeginsAndCountsTheMatch()
    {
        var duel = Duel();
        Assert.Empty(duel.Tick(PvpDuel.PrepareMs - 1));
        Assert.Equal(PvpDuelPhase.Prepare, duel.Phase);
        Assert.IsType<PvpDuelBattleBegan>(Assert.Single(duel.Tick(1)));
        var outcome = Finish(duel.Died(Defender));
        Assert.Equal(5, outcome.Seats[0].Record.Matches);
        Assert.Equal(7, outcome.Seats[1].Record.Matches);
    }

    [Fact]
    public void ADeathInBattleMakesTheOtherPlayerWinAndRevivesTheLoser()
    {
        var duel = Fighting();
        duel.Tick(12345);
        var outcome = Finish(duel.Died(Challenger));
        Assert.False(outcome.Cancelled);
        Assert.Equal(0, outcome.RevivedSeat);
        Assert.Equal(PvpDuelPhase.Finished, duel.Phase);
        var lost = outcome.Seats[0];
        var won = outcome.Seats[1];
        Assert.Equal(1u, won.Winner);
        Assert.Equal(1u, lost.Winner);
        Assert.True(won.Won);
        Assert.False(lost.Won);
        Assert.Equal(PvpDuelOutcome.CodeWin, won.ResultCode);
        Assert.Equal(PvpDuelOutcome.CodeLose, lost.ResultCode);
        Assert.Equal(15, won.LadderGain);
        Assert.Equal(-15, lost.LadderGain);
        Assert.Equal(new PvpRecord(1015, 7, 4, 2, 1), won.Record);
        Assert.Equal(new PvpRecord(985, 5, 2, 3, 0), lost.Record);
        Assert.Equal(12345u, won.ElapsedMs);
        Assert.False(won.TimedOut);
    }

    [Theory]
    [InlineData(1000, 1400, 27)]
    [InlineData(1400, 1000, 4)]
    [InlineData(1000, 1001, 15)]
    [InlineData(1000, 1000, 15)]
    [InlineData(1000, 1100, 18)]
    [InlineData(1100, 1000, 13)]
    public void TheWinnerGainsAndTheLoserLosesTheSamePointsFromTheRatingGap(int winner, int loser, int points)
    {
        Assert.Equal(points, PvpLadderRules.Points(winner, loser));
        var duel = Fighting(winner, loser);
        var outcome = Finish(duel.Died(Defender));
        Assert.Equal(-points, outcome.Seats[1].LadderGain);
        Assert.Equal(points, outcome.Seats[0].LadderGain);
        Assert.Equal(winner + points, outcome.Seats[0].Record.Ladder);
        Assert.Equal(loser - points, outcome.Seats[1].Record.Ladder);
    }

    [Fact]
    public void WhenTheTimeRunsOutTheChallengerLoses()
    {
        var duel = Fighting();
        Assert.Empty(duel.Tick(PvpDuel.BattleMs - 1));
        var outcome = Finish(duel.Tick(1));
        Assert.Null(outcome.RevivedSeat);
        Assert.True(outcome.Seats[0].TimedOut);
        Assert.True(outcome.Seats[1].TimedOut);
        Assert.Equal(PvpDuelOutcome.CodeLose, outcome.Seats[0].ResultCode);
        Assert.Equal(PvpDuelOutcome.CodeWin, outcome.Seats[1].ResultCode);
        Assert.Equal(1u, outcome.Seats[0].Winner);
        Assert.Equal((uint)PvpDuel.BattleMs, outcome.Seats[1].ElapsedMs);
        Assert.Equal(-15, outcome.Seats[0].LadderGain);
    }

    [Fact]
    public void LeavingInBattleGivesTheOtherPlayerTheWinAndCountsADisconnectWithoutLadderPoints()
    {
        var duel = Fighting();
        var outcome = Finish(duel.Left(Defender));
        Assert.Null(outcome.RevivedSeat);
        var winner = outcome.Seats[0];
        var leaver = outcome.Seats[1];
        Assert.Equal(PvpDuelOutcome.WinnerNone, winner.Winner);
        Assert.Equal(PvpDuelOutcome.CodeWin, winner.ResultCode);
        Assert.Equal(PvpDuelOutcome.CodeDisconnect, leaver.ResultCode);
        Assert.True(winner.Won);
        Assert.False(leaver.Won);
        Assert.Equal(0, winner.LadderGain);
        Assert.Equal(new PvpRecord(1000, 5, 3, 2, 0), winner.Record);
        Assert.Equal(new PvpRecord(1000, 7, 3, 2, 2), leaver.Record);
    }

    [Fact]
    public void LeavingWhilePreparingCancelsTheGameAndTellsTheOtherPlayer()
    {
        var duel = Duel();
        var events = duel.Left(Challenger);
        Assert.Equal(2, events.Count);
        Assert.Equal(new PvpDuelPrepareLost(1, 0), events[0]);
        var outcome = Assert.IsType<PvpDuelFinished>(events[1]).Outcome;
        Assert.True(outcome.Cancelled);
        Assert.Equal(PvpDuelPhase.Finished, duel.Phase);
        Assert.Equal(4, outcome.Seats[0].Record.Matches);
    }

    [Fact]
    public void ADeathWhilePreparingOnlyTellsTheOtherPlayer()
    {
        var duel = Duel();
        Assert.Equal(new PvpDuelPrepareLost(0, 1), Assert.Single(duel.Died(Defender)));
        Assert.Equal(PvpDuelPhase.Prepare, duel.Phase);
        Assert.IsType<PvpDuelBattleBegan>(Assert.Single(duel.Tick(PvpDuel.PrepareMs)));
    }

    [Fact]
    public void OutsidersAndFinishedGamesRaiseNothing()
    {
        var duel = Fighting();
        Assert.Empty(duel.Died(99));
        Assert.Empty(duel.Left(99));
        Finish(duel.Died(Challenger));
        Assert.Empty(duel.Died(Defender));
        Assert.Empty(duel.Left(Defender));
        Assert.Empty(duel.Tick(PvpDuel.BattleMs));
    }

    [Fact]
    public void SeatsAreFoundByCharacter()
    {
        var duel = Duel();
        Assert.Equal(0, duel.SeatOf(Challenger));
        Assert.Equal(1, duel.SeatOf(Defender));
        Assert.Equal(-1, duel.SeatOf(5));
        Assert.True(duel.Involves(Defender));
        Assert.Equal(Defender, duel.CharacterOf(1));
        Assert.Equal((ushort)7, duel.ZoneOf(0));
    }
}
