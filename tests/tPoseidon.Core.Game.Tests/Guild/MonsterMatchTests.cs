using tPoseidon.Core.Game.Guild;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Guild;

public sealed class MonsterMatchTests
{
    private static readonly MonsterMatchTables Points = new(new Dictionary<int, MonsterMatchEntry>
    {
        [0] = new(0, 2207, 1),
        [1] = new(1, 2210, 14),
        [3] = new(3, 2211, 20),
    });

    private static MonsterMatch Match(int goal = 30)
    {
        var match = new MonsterMatch(Points, goal);
        match.Join(1, 0);
        match.Join(2, 1);
        return match;
    }

    [Fact]
    public void OneSummonAtATimeSpawnedForBothTeams()
    {
        var match = Match();
        var summon = match.Touch(1)!;
        Assert.Equal(2210, summon.MonsterId);
        Assert.Null(match.Touch(3));
        Assert.Null(match.Touch(9));
        summon.Monsters[0] = 100;
        summon.Monsters[1] = 101;
        Assert.True(match.MonsterKilled(100, 1));
        Assert.True(match.MonsterKilled(101, 1));
        Assert.Empty(match.Summons);
        Assert.NotNull(match.Touch(3));
    }

    [Fact]
    public void OwnColourKillScoresWrongColourCounts()
    {
        var match = Match();
        var summon = match.Touch(1)!;
        summon.Monsters[0] = 100;
        summon.Monsters[1] = 101;
        match.MonsterKilled(100, 1);
        match.MonsterKilled(101, 1);
        Assert.Equal(14, match.TeamScore[0]);
        Assert.Equal(14, match.Fighters[0].Score);
        Assert.Equal(1, match.Fighters[0].WrongKills);
        Assert.Equal(0, match.TeamScore[1]);
    }

    [Fact]
    public void SummonsExpireAfterThreeHundredSeconds()
    {
        var match = Match();
        var summon = match.Touch(0)!;
        summon.Monsters[0] = 100;
        summon.Monsters[1] = 101;
        Assert.Empty(match.Tick(299_000));
        var expired = match.Tick(1_000);
        Assert.Equal(2, expired.Count);
        Assert.Empty(match.Summons);
    }

    [Fact]
    public void ScoreGoalAndMissingTeamEndAtOnce()
    {
        var match = Match(goal: 14);
        Assert.Null(match.InstantEnd());
        var summon = match.Touch(1)!;
        summon.Monsters[1] = 101;
        match.MonsterKilled(101, 2);
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerTeam1, GuildGameRules.ResultDecided, GuildGameRules.ReasonScoreGoal), match.InstantEnd());
        var lonely = Match();
        lonely.Leave(2);
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerTeam0, GuildGameRules.ResultDecided, GuildGameRules.ReasonNoFighters), lonely.InstantEnd());
    }

    [Fact]
    public void TimeUpBreaksTiesByKillsThenDeaths()
    {
        var match = Match();
        Assert.Equal(new MatchEnd(GuildGameRules.WinnerDraw, GuildGameRules.ResultNoScore, GuildGameRules.ReasonTimeUp), match.TimeUp());
        match.PlayerKilled(2, 1);
        Assert.Equal(GuildGameRules.WinnerTeam0, match.TimeUp().Winner);
        match.PlayerKilled(1, 2);
        match.PlayerKilled(1, 1);
        Assert.Equal(GuildGameRules.WinnerTeam1, match.TimeUp().Winner);
    }
}
