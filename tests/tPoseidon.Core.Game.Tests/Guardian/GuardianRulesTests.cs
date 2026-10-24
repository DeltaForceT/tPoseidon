using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianRulesTests
{
    private static GuardianTables Tables(Dictionary<int, int> nextExp) => new(
        nextExp,
        new Dictionary<int, GuardianParamEntry>(),
        new Dictionary<int, GuardianGrowthEntry>(),
        new Dictionary<int, GuardianBasicStatEntry>
        {
            [65] = new(65, [18, 0, 10, 80, 0, 0, 1200, 0, 0, 120, 0, 0]),
        },
        new Dictionary<int, float>());

    private static readonly GuardianTables Levels = Tables(new Dictionary<int, int> { [0] = 50, [1] = 100, [2] = 200, [3] = 300, [199] = 1000, [200] = 2000 });

    [Fact]
    public void ExpBelowTheThresholdStaysOnTheLevel()
    {
        Assert.Equal(new GuardianExpResult(1, 99, 0), GuardianRules.AddExp(Levels, 1, 40, 59));
    }

    [Fact]
    public void ExpCarriesOverAcrossSeveralLevels()
    {
        Assert.Equal(new GuardianExpResult(3, 50, 2), GuardianRules.AddExp(Levels, 1, 0, 350));
    }

    [Fact]
    public void TheCapResetsExpAndAMissingThresholdStops()
    {
        Assert.Equal(new GuardianExpResult(200, 0, 1), GuardianRules.AddExp(Levels, 199, 900, 500));
        Assert.Equal(new GuardianExpResult(200, 5, 0), GuardianRules.AddExp(Levels, 200, 5, 500));
        Assert.Equal(new GuardianExpResult(4, 10, 0), GuardianRules.AddExp(Levels, 4, 0, 10));
    }

    [Fact]
    public void TheThresholdTableAnswersOnlyLevelsOneToFourHundred()
    {
        Assert.Equal(0, GuardianRules.NextExp(Levels, 0));
        Assert.Equal(100, GuardianRules.NextExp(Levels, 1));
        Assert.Equal(0, GuardianRules.NextExp(Levels, 401));
    }

    [Fact]
    public void StatsScalePointsByLevelAndGrowthThenAddTheAccuracyAndMagicBonus()
    {
        short[] points = [100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100, 100];
        float[] ratios = [1f, 1f, 1f, 0.5f, 0.5f, 0.5f, 0.7f, 0.7f, 0.7f, 0.85f, 0.85f, 0.85f];

        var stats = GuardianRules.Stats(points, 30, ratios);

        Assert.Equal([30, 80, 30, 15, 65, 15, 21, 21, 21, 26, 26, 26], stats);
    }

    [Fact]
    public void SkillPointsMustMatchTheLevelAfterCardLevels()
    {
        Assert.Equal(new GuardianSkillPointCheck(5, false), GuardianRules.CheckSkillPoints(10, 5, [3, 4, 1]));
        Assert.Equal(new GuardianSkillPointCheck(10, true), GuardianRules.CheckSkillPoints(10, 7, [3, 4, 1]));
    }

    [Fact]
    public void CreationLevelIsTheSeedPointSumAndAtLeastOne()
    {
        Assert.Equal(13, GuardianRules.StartingLevel(4, 3, 3, 3));
        Assert.Equal(1, GuardianRules.StartingLevel(0, 0, 0, 0));
    }

    [Fact]
    public void BasicStatRowFollowsOwnerLevelAndRareType()
    {
        Assert.Equal(50, GuardianRules.BasicStatLevel(50, false, 0));
        Assert.Equal(65, GuardianRules.BasicStatLevel(50, false, 1));
        Assert.Equal(60, GuardianRules.BasicStatLevel(50, false, 2));
        Assert.Equal(100, GuardianRules.BasicStatLevel(50, true, 3));
        Assert.Equal((short)1200, GuardianRules.BasicPoints(Levels, 65)![6]);
        Assert.Null(GuardianRules.BasicPoints(Levels, 66));
    }
}
