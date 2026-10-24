using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillLevellingTests
{
    private static SkillParam2Entry Row(int maxLevel = 10, bool levelUp = true, int masterTm = 5, int card = 27001) =>
        new() { Id = 1001, SkillCardId = card, LevelUp = levelUp, MaxSkillLevel = maxLevel, MasterTmPoint = masterTm };

    [Fact]
    public void TheNextLevelWithTmPointsIsAccepted()
    {
        Assert.Null(SkillLevelling.Check(Row(), hasCard: true, currentLevel: 1, level: 2, tmPoints: 1));
    }

    [Fact]
    public void ASkillTheCharacterLacksIsRefused()
    {
        Assert.Equal(SkillLevelRefusal.NotOwned, SkillLevelling.Check(Row(), true, null, 2, 5));
    }

    [Fact]
    public void ASkillWithoutCardIsRefused()
    {
        Assert.Equal(SkillLevelRefusal.NoCard, SkillLevelling.Check(Row(), false, 1, 2, 5));
        Assert.Equal(SkillLevelRefusal.NoCard, SkillLevelling.Check(null, true, 1, 2, 5));
    }

    [Fact]
    public void ASkillAtLevelZeroIsRefused()
    {
        Assert.Equal(SkillLevelRefusal.NotLearned, SkillLevelling.Check(Row(), true, 0, 1, 5));
    }

    [Fact]
    public void ASkillThatDoesNotLevelIsRefused()
    {
        Assert.Equal(SkillLevelRefusal.NotLevelling, SkillLevelling.Check(Row(levelUp: false), true, 1, 2, 5));
    }

    [Fact]
    public void TheMaxLevelOfTheTableIsTheLimit()
    {
        Assert.Equal(SkillLevelRefusal.AboveMaxLevel, SkillLevelling.Check(Row(maxLevel: 1), true, 1, 2, 5));
    }

    [Fact]
    public void OnlyTheNextLevelCanBeAsked()
    {
        Assert.Equal(SkillLevelRefusal.NotNextLevel, SkillLevelling.Check(Row(), true, 1, 3, 5));
    }

    [Fact]
    public void LevelsUpToTenNeedOneTmPoint()
    {
        Assert.Equal(SkillLevelRefusal.NoTmPoints, SkillLevelling.Check(Row(), true, 9, 10, 0));
    }

    [Fact]
    public void LevelElevenNeedsTheMasterTmPoints()
    {
        Assert.Equal(SkillLevelRefusal.NotEnoughTmForMaster, SkillLevelling.Check(Row(maxLevel: 11, masterTm: 5), true, 10, 11, 4));
        Assert.Null(SkillLevelling.Check(Row(maxLevel: 11, masterTm: 5), true, 10, 11, 5));
    }

    [Fact]
    public void NoLevelAboveElevenExists()
    {
        Assert.Equal(SkillLevelRefusal.AboveMasterLevel, SkillLevelling.Check(Row(maxLevel: 12), true, 11, 12, 50));
    }

    [Fact]
    public void ACardCountsWhenItsRowExists()
    {
        var cards = new Dictionary<int, SkillCardEntry> { [27001] = new SkillCardEntry { Id = 27001, AffectionSkill = 1001 } };

        Assert.True(SkillLevelling.HasCard(Row(card: 27001), cards));
        Assert.False(SkillLevelling.HasCard(Row(card: 27002), cards));
        Assert.False(SkillLevelling.HasCard(Row(card: 0), cards));
    }
}
