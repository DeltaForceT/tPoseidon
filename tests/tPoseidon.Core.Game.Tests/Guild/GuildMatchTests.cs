using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;

namespace tPoseidon.Core.Game.Tests.Guild;

public sealed class GuildMatchTests
{
    private static GuildMatch TwoSides(byte type)
    {
        var match = new GuildMatch(7, type, 179, 20, 0);
        Assert.Equal(0, match.AddSide(100, "A", 1, new GuildLeagueRecord(3, 2, 1)));
        Assert.Equal(1, match.AddSide(200, "B", 2, GuildLeagueRecord.None));
        Assert.Equal(new GuildLeagueRecord(3, 2, 1), match.Sides[0].League);
        match.Enter(1, 0);
        match.Enter(11, 0);
        match.Enter(2, 1);
        match.Enter(22, 1);
        return match;
    }

    [Fact]
    public void BothMastersReadyStartsTheFiveSecondCountdownThenTheBattle()
    {
        var match = TwoSides(GuildGameRules.TeamMatch);
        Assert.Null(match.ToggleReady(11, false));
        Assert.Equal(new GuildMatchReadyChanged(true, false), match.ToggleReady(1, false));
        Assert.Equal(new GuildMatchReadyChanged(true, true), match.ToggleReady(2, false));
        Assert.Empty(match.Tick(499));
        Assert.Empty(match.Tick(1));
        Assert.Equal(GuildMatchState.Countdown, match.State);
        var seconds = new List<ushort>();
        var began = false;
        for (var tick = 0; tick < 6; tick++)
        {
            foreach (var raised in match.Tick(1000))
            {
                if (raised is GuildMatchCountdown countdown) seconds.Add(countdown.SecondsLeft);
                if (raised is GuildMatchBattleBegan) began = true;
            }
        }
        Assert.Equal(new ushort[] { 5, 4, 3, 2, 1, 0 }, seconds);
        Assert.True(began);
        Assert.Equal(GuildMatchState.Battle, match.State);
        Assert.Equal(1200, match.SecondsLeft);
        Assert.Equal(3, GuildMatch.PvpModeOf(0));
        Assert.Equal(4, GuildMatch.PvpModeOf(1));
    }

    private static GuildMatch Battling(byte type)
    {
        var match = TwoSides(type);
        match.ToggleReady(1, false);
        match.ToggleReady(2, false);
        match.Tick(500);
        for (var tick = 0; tick < 6; tick++) match.Tick(1000);
        Assert.Equal(GuildMatchState.Battle, match.State);
        return match;
    }

    [Fact]
    public void TeamMatchTimeUpGoesToTheHigherKillCount()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.PlayerKilled(2, 1);
        var ended = match.Tick(1200 * 1000).OfType<GuildMatchEnded>().Single();
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerTeam0, GuildGameRules.ResultDecided, GuildGameRules.ReasonTimeUp), ended.End);
        Assert.Equal(GuildMatchState.Result, match.State);
        Assert.Equal(0, GuildMatch.WinnerTeam(ended.End));
    }

    [Fact]
    public void TeamMatchDrawWithoutDeathsIsResultZero()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        var ended = match.Tick(1200 * 1000).OfType<GuildMatchEnded>().Single();
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerDraw, GuildGameRules.ResultNoScore, GuildGameRules.ReasonTimeUp), ended.End);
    }

    [Fact]
    public void DeathMatchEndsWhenASideHasNobodyStanding()
    {
        var match = Battling(GuildGameRules.DeathMatch);
        match.PlayerKilled(2, 1);
        Assert.Empty(match.Tick(1000).OfType<GuildMatchEnded>());
        match.PlayerKilled(22, 11);
        var ended = match.Tick(1000).OfType<GuildMatchEnded>().Single();
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerTeam0, GuildGameRules.ResultDecided, GuildGameRules.ReasonNoFighters), ended.End);
    }

    [Fact]
    public void ResultLastsTwoMinutesThenCloses()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.Tick(1200 * 1000);
        Assert.Empty(match.Tick(119 * 1000).OfType<GuildMatchClosing>());
        Assert.Single(match.Tick(1000).OfType<GuildMatchClosing>());
    }

    [Fact]
    public void AnEmptyBattleIsInvalid()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        foreach (var id in new ulong[] { 1, 11, 2 }) match.Leave(id);
        Assert.True(match.BadQuit(22));
        Assert.Single(match.Tick(1000).OfType<GuildMatchInvalid>());
        Assert.Equal(1, match.Sides[1].BadQuits);
    }

    [Theory]
    [InlineData(0, false, true)]
    [InlineData(1, true, true)]
    [InlineData(GuildMatch.EventRoomLadderFlag, true, false)]
    public void TheLadderFlagMarksTheMatchAndAnEventRoomWaitsForTheGameMaster(byte flag, bool noLadder, bool gmReady)
    {
        var match = new GuildMatch(7, GuildGameRules.TeamMatch, 179, 20, flag);

        Assert.Equal(noLadder, match.NoLadderFlag != 0);
        Assert.Equal(gmReady, match.GmReady);
    }
}
