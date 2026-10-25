using tPoseidon.Core.Game.SoulSeed;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.SoulSeed;

public sealed class SoulSeedGrowthTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
    private static readonly short[] ExpectedSession = [2, 1, 0, 0, 0, 0, 0, 0];
    private static readonly int[] UnevenSums = [5, 3, 9, 9];
    private static readonly int[] EvenSums = [4, 4, 4, 4];
    private static readonly int[] EvenTwos = [2, 2, 2, 2];
    private static readonly int[] EvenThrees = [3, 3, 3, 3];
    private static readonly int[] Uneven = [5, 4, 3, 1];
    private static readonly int[] UnevenLow = [5, 4, 2, 1];

    private static SoulSeedEventEntry Row(int eventType, int rate, int maxLevel, bool special, params int[] points) =>
        new(eventType + 1, eventType, 0, 0, special, maxLevel, rate, points);

    private static SoulSeedRecord Saved(params short[] points) => new(true, 38000, Now, Now, points);

    private static SoulSeedGrowth Planted()
    {
        var seed = new SoulSeedGrowth();
        seed.Plant(38000, Now);
        return seed;
    }

    [Fact]
    public void NothingHappensWithoutASeedButTheEventBitIsSet()
    {
        var seed = new SoulSeedGrowth();
        Assert.False(seed.OnEvent(10, 1, Row(10, 5, 5, false, 2, 1, 0, 0)).Processed);
        Assert.Equal(1UL << 10, seed.EventMask);
    }

    [Fact]
    public void BelowTheRateOnlyTheCounterGrows()
    {
        var seed = Planted();
        var row = Row(10, 5, 5, false, 2, 1, 0, 0);
        for (var i = 1; i < 5; i++)
        {
            var outcome = seed.OnEvent(10, 1, row);
            Assert.False(outcome.Granted);
            Assert.Equal(i, outcome.Counter);
        }
        var granted = seed.OnEvent(10, 1, row);
        Assert.True(granted.Granted);
        Assert.Equal(0, seed.Counters[10]);
        Assert.Equal(ExpectedSession, seed.SessionPoints);
    }

    [Fact]
    public void FiftyPointsGiveABuffAndAThousandBackUpTheSession()
    {
        var seed = Planted();
        var row = Row(0, 1, 0, false, 20, 20, 20, 20);
        var outcome = SoulSeedEventOutcome.Skipped;
        for (var i = 0; i < 3; i++) outcome = seed.OnEvent(0, 1, row);
        Assert.True(outcome.BuffChanged);
        Assert.Equal(3, seed.Buff().Group);
        Assert.Equal(SoulSeedRules.BuffMilliseconds, seed.Buff().MillisecondsLeft);
        for (var i = 0; i < 47; i++) outcome = seed.OnEvent(0, 1, row);
        Assert.Equal(4, outcome.Backups.Count);
        Assert.Equal(1000, seed.Record().SavedPoints[0]);
        Assert.All(seed.SessionPoints, point => Assert.Equal(0, point));
        Assert.Equal(SoulSeedRules.MaxBuffStacks, seed.Buff().Stacks);
    }

    [Fact]
    public void TheBuffRunsOutAfterItsMilliseconds()
    {
        var seed = Planted();
        var row = Row(0, 1, 0, false, 20, 20, 20, 20);
        for (var i = 0; i < 3; i++) seed.OnEvent(0, 1, row);
        Assert.False(seed.TickBuff(SoulSeedRules.BuffMilliseconds - 1));
        Assert.Equal(1, seed.Buff().MillisecondsLeft);
        Assert.True(seed.TickBuff(1));
        Assert.Equal(SoulSeedRules.NoBuffGroup, seed.Buff().Group);
        Assert.Equal(0, seed.Buff().Stacks);
        Assert.False(seed.TickBuff(1000));
    }

    [Fact]
    public void MaximumLevelCapsNormalPointsAndSixLevelsComplete()
    {
        var seed = Planted();
        seed.Restore(Saved(1999, 0, 0, 0, 0, 0, 0, 0));
        var capped = Row(2, 1, 2, false, 8, 0, 0, 0);
        seed.OnEvent(2, 1, capped);
        Assert.Equal(2000, seed.Total(0));
        seed.OnEvent(2, 1, capped);
        Assert.Equal(2000, seed.Total(0));

        seed.Restore(Saved(5999, 0, 0, 0, 0, 0, 0, 0));
        var outcome = seed.OnEvent(0, 1, Row(0, 1, 0, false, 20, 0, 0, 0));
        Assert.True(outcome.Complete);
        Assert.Equal(6000, seed.Total(0));
        Assert.True(seed.Complete);
        seed.OnEvent(0, 1, Row(0, 1, 0, false, 0, 20, 0, 0));
        Assert.Equal(0, seed.Total(1));
    }

    [Fact]
    public void SpecialPointsStopAboveLevelOne()
    {
        var seed = Planted();
        seed.Restore(Saved(0, 0, 0, 0, 1999, 0, 0, 0));
        seed.OnEvent(41, 1, Row(41, 1, 0, true, 20, 0, 0, 0));
        Assert.Equal(2000, seed.Total(4));
        seed.OnEvent(41, 1, Row(41, 1, 0, true, 20, 0, 0, 0));
        Assert.Equal(2000, seed.Total(4));
    }

    [Fact]
    public void GuardianKindIsTheHighestSumAndRareTypeFollowsTheLevels()
    {
        Assert.Equal(2, SoulSeedRules.GuardianKind(UnevenSums, _ => 0));
        Assert.Equal(3, SoulSeedRules.GuardianKind(EvenSums, _ => 3));
        Assert.Equal(4, SoulSeedRules.RareType(EvenTwos, 8, 0));
        Assert.Equal(3, SoulSeedRules.RareType(EvenThrees, 12, 0));
        Assert.Equal(2, SoulSeedRules.RareType(Uneven, 13, 3));
        Assert.Equal(1, SoulSeedRules.RareType(Uneven, 13, 4));
        Assert.Equal(0, SoulSeedRules.RareType(UnevenLow, 12, 4));
    }

    [Fact]
    public void QuestTypesMapToTheirEvents()
    {
        Assert.Equal(6, SoulSeedRules.QuestEvent(1));
        Assert.Equal(8, SoulSeedRules.QuestEvent(2));
        Assert.Equal(7, SoulSeedRules.QuestEvent(8));
        Assert.Equal(9, SoulSeedRules.QuestEvent(9));
        Assert.Null(SoulSeedRules.QuestEvent(7));
        Assert.True(SoulSeedRules.CountsKill(30, 11));
        Assert.False(SoulSeedRules.CountsKill(30, 10));
    }
}
