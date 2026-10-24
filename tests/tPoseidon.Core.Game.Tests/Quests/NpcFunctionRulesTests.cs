using tPoseidon.Core.Game.Quests;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class NpcFunctionRulesTests
{
    private static readonly GuildSetupRequirement Commercial = new(35, 2_000_000);

    [Theory]
    [InlineData(34, 5_000_000, GuildSetupRules.LevelLow)]
    [InlineData(35, 1_999_999, GuildSetupRules.GelderShort)]
    [InlineData(35, 2_000_000, GuildSetupRules.Ready)]
    public void GuildSetupTestsTheRowFirst(int level, long money, int expected)
    {
        Assert.Equal(expected, GuildSetupRules.Check(Commercial, level, money));
    }

    [Theory]
    [InlineData(34, 300, GuildSetupRules.LevelLow)]
    [InlineData(40, 199, GuildSetupRules.GelderShort)]
    [InlineData(40, 200, GuildSetupRules.Ready)]
    public void GuildSetupAlsoNeedsTheFixedLevelAndGelder(int level, long money, int expected)
    {
        Assert.Equal(expected, GuildSetupRules.Check(new GuildSetupRequirement(1, 1), level, money));
    }

    [Theory]
    [InlineData(2, true, 1)]
    [InlineData(2, false, 2)]
    [InlineData(1, true, NpcScript.Refused)]
    [InlineData(4, true, NpcScript.Refused)]
    public void SympathizingNeedsTwoArgumentsAndAnswersThePlantedSeed(int args, bool planted, int expected)
    {
        Assert.Equal(expected, SympathizingRules.Result(args, planted));
    }

    [Theory]
    [InlineData(21u, NpcScriptOperation.NotifyCardBattleRank, 0)]
    [InlineData(53u, NpcScriptOperation.GuildSetupCheck, 0)]
    [InlineData(54u, NpcScriptOperation.GuildWindow, 54)]
    [InlineData(58u, NpcScriptOperation.GuildWindow, 58)]
    [InlineData(79u, NpcScriptOperation.GuildWindow, 79)]
    [InlineData(81u, NpcScriptOperation.GuildWindow, 81)]
    [InlineData(223u, NpcScriptOperation.EventTalk, NpcEventTalk.Talk)]
    [InlineData(254u, NpcScriptOperation.EventTalk, NpcEventTalk.Scene)]
    [InlineData(224u, NpcScriptOperation.CheckSympathizing, 0)]
    [InlineData(117u, NpcScriptOperation.WarpGate, 0)]
    [InlineData(257u, NpcScriptOperation.WarpGate, 1)]
    [InlineData(219u, NpcScriptOperation.MonsterSummon, 0)]
    [InlineData(88u, NpcScriptOperation.LimitTeleport, 300)]
    [InlineData(110u, NpcScriptOperation.LimitTeleport, 50)]
    [InlineData(227u, NpcScriptOperation.LimitTeleport, 30)]
    public void TheNewFunctionsAreClassified(uint functionId, NpcScriptOperation operation, int slot)
    {
        Assert.Equal(new NpcScriptCall(operation, slot), NpcScript.Classify(functionId));
    }

    [Theory]
    [InlineData(9, 239)]
    [InlineData(18, 239)]
    [InlineData(25, 242)]
    [InlineData(33, 246)]
    public void TheWarpGateSendsEachJobToItsGuardianRoom(int job, int zone)
    {
        Assert.Equal(zone, WarpGateRules.GuardianRoomOfJob(job));
    }

    [Fact]
    public void TheSecondWarpGatePicksTheRoomOfTheSpecies()
    {
        Assert.Null(WarpGateRules.GuardianRoomOfJob(8));
        Assert.Equal(628, WarpGateRules.GuardianRoomOfSpecies(9, 1));
        Assert.Equal(635, WarpGateRules.GuardianRoomOfSpecies(20, 8));
        Assert.Equal(754, WarpGateRules.GuardianRoomOfSpecies(33, 9));
        Assert.Null(WarpGateRules.GuardianRoomOfSpecies(5, 1));
    }

    [Fact]
    public void ASummonNeedsFewerMonstersThanTheMapAverage()
    {
        Assert.True(MonsterSummonRules.HasRoom(0, 1));
        Assert.False(MonsterSummonRules.HasRoom(1, 1));
        Assert.False(MonsterSummonRules.HasRoom(0, 0));
    }

    [Theory]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(4, false)]
    public void SummonedMonstersTargetTheSummonerOnlyOnClassTwoMaps(int mapClass, bool targets)
    {
        Assert.Equal(targets, MonsterSummonRules.TargetsSummoner(mapClass));
    }

    [Fact]
    public void FunctionTwoFiftyEightStartsTheNpcBufferEffectWithoutABody()
    {
        Assert.Equal(NpcScriptOperation.NpcSkillStart, NpcScript.Classify(NpcSkillRules.Function).Operation);
        var kind = tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.Find(tPoseidon.Core.Game.Skills.Effects.SkillEffectKinds.NpcBuffer);
        Assert.NotNull(kind);
        Assert.Equal(tPoseidon.Core.Game.Skills.Effects.SkillEffectTail.Empty, kind.Tail);
        Assert.False(kind.OnTarget);
        Assert.False(kind.ChangesStat);
    }

    [Fact]
    public void APartyZoneStaysClosedUntilItsReEnterTimeAfterTheBossKill()
    {
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var bosses = new HashSet<int> { 2072 };
        Assert.Equal(15, PartyZoneRules.MinutesLeft(20, bosses, new[] { (2072, now.AddMinutes(-5).AddSeconds(-30)) }, now));
        Assert.Null(PartyZoneRules.MinutesLeft(20, bosses, new[] { (2072, now.AddMinutes(-20)) }, now));
        Assert.Null(PartyZoneRules.MinutesLeft(20, bosses, new[] { (9999, now) }, now));
        Assert.Equal(new NpcScriptCall(NpcScriptOperation.CheckEnterPartyZone, 0), NpcScript.Classify(218));
    }

    [Fact]
    public void ALimitedTeleportNeedsAZoneCountBelowTheLimit()
    {
        Assert.True(LimitTeleportRules.HasRoom(49, 50));
        Assert.False(LimitTeleportRules.HasRoom(50, 50));
        Assert.False(LimitTeleportRules.HasRoom(null, 50));
    }
}
