using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;

namespace tPoseidon.Core.Game.Tests.Guild;

public sealed class GuildMatchRespawnTests
{
    private static GuildMatch Battling(byte type)
    {
        var match = new GuildMatch(7, type, 179, 20, 0);
        match.AddSide(100, "A", 1, GuildLeagueRecord.None);
        match.AddSide(200, "B", 2, GuildLeagueRecord.None);
        match.Enter(1, 0);
        match.Enter(11, 0);
        match.Enter(2, 1);
        match.Enter(22, 1);
        match.ToggleReady(1, false);
        match.ToggleReady(2, false);
        match.Tick(500);
        for (var tick = 0; tick < 6; tick++) match.Tick(1000);
        Assert.Equal(GuildMatchState.Battle, match.State);
        return match;
    }

    private static List<ulong> RespawnsWithin(GuildMatch match, int seconds)
    {
        var respawned = new List<ulong>();
        for (var second = 0; second < seconds; second++)
        {
            foreach (var raised in match.Tick(1000).OfType<GuildMatchRespawn>()) respawned.AddRange(raised.CharacterIds);
        }
        return respawned;
    }

    [Fact]
    public void ADeathNearAWaveMarkWaitsThreeSecondsMoreAndFlagsTheNotice()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        Assert.Equal(1200, match.SecondsLeft);
        var kill = match.PlayerKilled(2, 1);
        Assert.Equal(new GuildMatchKill(1, 2, 1), kill);
        Assert.Empty(RespawnsWithin(match, 2));
        Assert.Equal(new ulong[] { 2 }, RespawnsWithin(match, 1));
    }

    [Fact]
    public void ADeathAwayFromAWaveMarkWaitsForTheNextThirtySecondMark()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.Tick(10 * 1000);
        Assert.Equal(1190, match.SecondsLeft);
        Assert.Equal(new GuildMatchKill(1, 2, 0), match.PlayerKilled(2, 1));
        Assert.Empty(RespawnsWithin(match, 19));
        Assert.Equal(new ulong[] { 2 }, RespawnsWithin(match, 1));
        Assert.Equal(1170, match.SecondsLeft);
    }

    [Fact]
    public void SeveralDeathsBeforeTheSameMarkRiseTogether()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.Tick(10 * 1000);
        match.PlayerKilled(2, 1);
        match.Tick(5 * 1000);
        match.PlayerKilled(22, 11);
        Assert.Empty(RespawnsWithin(match, 14));
        Assert.Equal(new ulong[] { 2, 22 }, RespawnsWithin(match, 1));
    }

    [Fact]
    public void DeathMatchPlayersStayDownAndTheNoticeHasNoRespawnFlag()
    {
        var match = Battling(GuildGameRules.DeathMatch);
        Assert.Equal(new GuildMatchKill(1, 2, 0), match.PlayerKilled(2, 1));
        Assert.True(match.IsDown(2));
        Assert.Empty(RespawnsWithin(match, 60));
    }

    [Fact]
    public void LeavingCancelsAPendingRespawn()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.PlayerKilled(2, 1);
        match.Leave(2);
        Assert.Empty(RespawnsWithin(match, 10));
    }

    [Fact]
    public void OnlyFightersDieInsideTheBattle()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        Assert.Null(match.PlayerKilled(99, 1));
        var room = new GuildMatch(8, GuildGameRules.TeamMatch, 179, 20, 0);
        Assert.Null(room.PlayerKilled(2, 1));
    }

    [Fact]
    public void KillsAndDeathsAreCounted()
    {
        var match = Battling(GuildGameRules.TeamMatch);
        match.PlayerKilled(2, 1);
        match.PlayerKilled(1, 22);
        Assert.All(match.TeamKills, kills => Assert.Equal(1, kills));
        Assert.All(match.TeamDeaths, deaths => Assert.Equal(1, deaths));
        Assert.Equal(1, match.FighterOf(1)!.Kills);
        Assert.Equal(1, match.FighterOf(1)!.Deaths);
    }
}
