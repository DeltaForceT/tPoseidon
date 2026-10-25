using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianGrowthTests
{
    private static readonly Dictionary<int, int> NextExp = new()
    {
        [1] = 100,
        [2] = 200,
        [3] = 300,
        [199] = 5000,
        [200] = 6000,
    };

    private static GuardianState At(short level, int exp, int skillPoint = 0, params GuardianSkill[] skills) =>
        new(0x55, 0, 15000, "g", level, 0, 0, 0, 0, 0, 0, 0, new short[12], exp, 0, skillPoint, skills);

    [Theory]
    [InlineData(250, 0.1f, 25)]
    [InlineData(19, 0.1f, 1)]
    [InlineData(9, 0.1f, 0)]
    [InlineData(0, 0.1f, 0)]
    [InlineData(-5, 0.1f, 0)]
    public void GainIsTheOwnerExpTimesTheRatioTruncated(int ownerExp, float ratio, int expected)
    {
        Assert.Equal(expected, GuardianGrowth.Gain(ownerExp, ratio));
    }

    [Fact]
    public void ExpBelowTheThresholdOnlyAccumulates()
    {
        var outcome = GuardianGrowth.AddExp(At(1, 40), 30, NextExp);

        Assert.Equal(new GuardianExpOutcome(1, 70, 30, 0), outcome);
        Assert.False(outcome!.Value.LeveledUp);
    }

    [Fact]
    public void ReachingTheThresholdCarriesTheRemainderIntoTheNextLevel()
    {
        var outcome = GuardianGrowth.AddExp(At(1, 90), 30, NextExp);

        Assert.Equal(new GuardianExpOutcome(2, 20, 30, 1), outcome);
        Assert.True(outcome!.Value.LeveledUp);
    }

    [Fact]
    public void OneGainCanRaiseSeveralLevels()
    {
        var outcome = GuardianGrowth.AddExp(At(1, 0), 450, NextExp);

        Assert.Equal(new GuardianExpOutcome(3, 150, 450, 2), outcome);
    }

    [Fact]
    public void AMissingThresholdRefusesTheGain()
    {
        Assert.Null(GuardianGrowth.AddExp(At(3, 299), 5, NextExp));
        Assert.Null(GuardianGrowth.AddExp(At(1, 0), 5, new Dictionary<int, int>()));
    }

    [Fact]
    public void TheLevelStopsAtTheCapAndTheExpIsCleared()
    {
        var outcome = GuardianGrowth.AddExp(At(199, 4990), 100, NextExp);

        Assert.Equal(new GuardianExpOutcome(200, 0, 100, 1), outcome);
        Assert.Null(GuardianGrowth.AddExp(At(200, 0), 100, NextExp));
    }

    [Fact]
    public void ApplyKeepsTheNewLevelExpAndAddsOneSkillPointPerLevel()
    {
        var grown = GuardianGrowth.Apply(At(1, 0, 4), new GuardianExpOutcome(3, 150, 450, 2));

        Assert.Equal(3, grown.Level);
        Assert.Equal(150, grown.Exp);
        Assert.Equal(6, grown.SkillPoint);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    [InlineData(200, 6000)]
    [InlineData(401, 0)]
    [InlineData(7, 0)]
    public void ThresholdReadsTheLevelTableWithinItsRange(int level, int expected)
    {
        Assert.Equal(expected, GuardianGrowth.Threshold(NextExp, level));
    }

    [Fact]
    public void LoginKeepsAConsistentGuardian()
    {
        var card = new GuardianSkill(4101, GuardianSkillType.Card, 0, 3, 0);
        var guardian = At(5, 40, 3, card);

        Assert.Equal(guardian, GuardianLoginCheck.Validate(guardian, NextExp));
    }

    [Fact]
    public void LoginResetsSkillPointsAndCardLevelsWhenTheyDoNotAddUpToTheLevel()
    {
        var card = new GuardianSkill(4101, GuardianSkillType.Card, 0, 5, 0);
        var special = new GuardianSkill(12000, GuardianSkillType.Special, 0, 4, 1);

        var corrected = GuardianLoginCheck.Validate(At(5, 40, 3, card, special), NextExp);

        Assert.Equal(5, corrected.SkillPoint);
        Assert.Equal(1, corrected.Skills.Single(skill => skill.Type == GuardianSkillType.Card).Level);
        Assert.Equal(4, corrected.Skills.Single(skill => skill.Type == GuardianSkillType.Special).Level);
    }

    [Fact]
    public void LoginClearsExpAtOrAboveTheThreshold()
    {
        Assert.Equal(0, GuardianLoginCheck.Validate(At(1, 100, 1), NextExp).Exp);
        Assert.Equal(99, GuardianLoginCheck.Validate(At(1, 99, 1), NextExp).Exp);
    }
}
