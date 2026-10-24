using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillCardRulesTests
{
    private static SkillCardEntry Card(int cardType = 6, int tmLevel = 50, int subType = 0, int subTmLevel = 0, int points = 4, params SkillRequirement[] pre) => new()
    {
        Id = 27109,
        CardType = cardType,
        TmLevel = tmLevel,
        SubCardType = subType,
        SubTmLevel = subTmLevel,
        AffectionPoint = points,
        AffectionSkill = 1109,
        RegisterLevel = 1,
        PreSkills = pre,
    };

    private static readonly Dictionary<int, int> NoSkills = new();

    [Theory]
    [InlineData(2, 1)]
    [InlineData(4, 10)]
    [InlineData(6, 19)]
    [InlineData(8, 22)]
    [InlineData(9, 21)]
    [InlineData(20, 26)]
    [InlineData(29, 31)]
    [InlineData(37, 32)]
    [InlineData(39, 35)]
    [InlineData(43, 9)]
    [InlineData(44, 18)]
    [InlineData(45, 36)]
    [InlineData(1, 0)]
    [InlineData(41, 0)]
    public void CardTypeMapsToTheJobTypeId(int cardType, int jobType)
    {
        Assert.Equal(jobType, SkillCardRules.JobTypeOf(cardType));
    }

    [Fact]
    public void ParentJobsWalkBackToTheBaseJob()
    {
        Assert.Equal(10, SkillCardRules.ParentOf(19));
        Assert.Equal(1, SkillCardRules.ParentOf(10));
        Assert.Equal(0, SkillCardRules.ParentOf(1));
        Assert.Equal(13, SkillCardRules.ParentOf(27));
        Assert.Equal(18, SkillCardRules.ParentOf(36));
        Assert.Equal(9, SkillCardRules.ParentOf(18));
    }

    [Fact]
    public void JobPassesForTheJobOrALaterJobOfItsLineWithEnoughTmLevel()
    {
        Assert.True(SkillCardRules.PairPasses(1, 1, 2, 1));
        Assert.True(SkillCardRules.PairPasses(19, 50, 2, 1));
        Assert.True(SkillCardRules.PairPasses(19, 50, 6, 50));
        Assert.False(SkillCardRules.PairPasses(10, 50, 6, 50));
        Assert.False(SkillCardRules.PairPasses(19, 49, 6, 50));
        Assert.False(SkillCardRules.PairPasses(3, 99, 2, 1));
        Assert.True(SkillCardRules.JobPasses(Card(cardType: 0), 3, 0));
        Assert.True(SkillCardRules.JobPasses(Card(cardType: 7, subType: 6, subTmLevel: 60), 19, 60));
    }

    [Fact]
    public void RefusalsFollowTheServerOrder()
    {
        Assert.Equal(SkillCardRules.AlreadyOwnedNotice, SkillCardRules.Refusal(Card(), new Dictionary<int, int> { [1109] = 1 }, 19, 50, 10));
        Assert.Equal(SkillCardRules.JobNotice, SkillCardRules.Refusal(Card(), NoSkills, 10, 50, 10));
        Assert.Equal(SkillCardRules.TmPointsNotice, SkillCardRules.Refusal(Card(), NoSkills, 19, 50, 3));
        Assert.Equal(SkillCardRules.PreSkillNotice, SkillCardRules.Refusal(Card(pre: new SkillRequirement(1004, 11)), new Dictionary<int, int> { [1004] = 10 }, 19, 50, 10));
        Assert.Null(SkillCardRules.Refusal(Card(pre: new SkillRequirement(1004, 11)), new Dictionary<int, int> { [1004] = 11 }, 19, 50, 4));
    }
}
