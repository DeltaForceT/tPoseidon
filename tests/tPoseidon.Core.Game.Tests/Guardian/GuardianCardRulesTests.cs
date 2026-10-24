using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianCardRulesTests
{
    [Theory]
    [InlineData(2, 0)]
    [InlineData(9, 0)]
    [InlineData(12, 1)]
    [InlineData(20, 1)]
    [InlineData(23, 2)]
    [InlineData(30, 2)]
    [InlineData(33, 3)]
    [InlineData(40, 3)]
    [InlineData(1, GuardianCardRules.NoGroup)]
    [InlineData(10, GuardianCardRules.NoGroup)]
    [InlineData(21, GuardianCardRules.NoGroup)]
    [InlineData(41, GuardianCardRules.NoGroup)]
    [InlineData(43, GuardianCardRules.NoGroup)]
    public void GroupFollowsTheCardTypeRanges(int cardType, byte group)
    {
        Assert.Equal(group, GuardianCardRules.GroupOf(cardType));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    [InlineData(11, 1)]
    [InlineData(-3, 1)]
    public void StoredLevelIsForcedToOneOutsideOneToTen(int level, int stored)
    {
        Assert.Equal(stored, GuardianCardRules.StoredLevel(level));
    }

    [Fact]
    public void ReserveUsesTheCardSkillGroupAndStoredLevelWhileRawKeepsTheLevel()
    {
        var card = new SkillCardEntry { Id = 27001, AffectionSkill = 2033, CardType = 13 };
        var record = new GuardianCardRecord(27001, 0x10, 11, 3);

        Assert.Equal(new GuardianSkill(2033, GuardianSkillType.Card, 1, 1, 3), GuardianCardRules.Reserve(card, record));
        Assert.Equal(new GuardianSkill(2033, GuardianSkillType.Card, 1, 11, 3), GuardianCardRules.Raw(card, record));
        Assert.Null(GuardianCardRules.Reserve(null, record));
        Assert.Null(GuardianCardRules.Raw(null, record));
    }

    [Fact]
    public void ReplaceKeepsTheOtherSkillTypesAndSetsThePoints()
    {
        var order = new GuardianSkill(1, GuardianSkillType.Order, 0, 1, 0);
        var oldCard = new GuardianSkill(2, GuardianSkillType.Card, 0, 1, 0);
        var guardian = new GuardianState(5, 0, 1, "g", 1, 0, 0, 0, 0, 0, 0, 0, new short[12], 0, 0, 9, [oldCard, order]);
        var card = new GuardianSkill(3, GuardianSkillType.Card, 2, 4, 1);

        var updated = GuardianCardRules.Replace(guardian, 4, [card]);

        Assert.Equal(4, updated.SkillPoint);
        Assert.Equal([order, card], updated.Skills);
    }

    [Fact]
    public void SlotsAboveAByteAreNotStorable()
    {
        Assert.True(GuardianCardRules.Storable(new GuardianCardRecord(1, 0, 1, 255)));
        Assert.False(GuardianCardRules.Storable(new GuardianCardRecord(1, 0, 1, 256)));
        Assert.False(GuardianCardRules.Storable(new GuardianCardRecord(1, 0, 1, -1)));
    }
}
