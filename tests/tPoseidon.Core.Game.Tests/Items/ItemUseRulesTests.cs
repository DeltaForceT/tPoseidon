using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemUseRulesTests
{
    private static ItemParamEntry Potion(int hp, int mp) => new()
    {
        Id = 2001,
        Type = ItemParamEntry.PotionType,
        Options = ItemParamEntry.UsableOption,
        Hp = hp,
        Mp = mp,
        MinLevel = 3,
    };

    [Theory]
    [InlineData(1, ItemUseHelper.Money)]
    [InlineData(10, ItemUseHelper.Potion)]
    [InlineData(11, ItemUseHelper.Card)]
    [InlineData(14, ItemUseHelper.Misc)]
    [InlineData(15, ItemUseHelper.Use)]
    [InlineData(16, ItemUseHelper.Throw)]
    [InlineData(28, ItemUseHelper.SkillCard)]
    [InlineData(29, ItemUseHelper.Bullet)]
    [InlineData(2, ItemUseHelper.Refuses)]
    [InlineData(6, ItemUseHelper.Refuses)]
    [InlineData(13, ItemUseHelper.Refuses)]
    [InlineData(17, ItemUseHelper.Refuses)]
    [InlineData(27, ItemUseHelper.Refuses)]
    [InlineData(34, ItemUseHelper.Refuses)]
    [InlineData(9, ItemUseHelper.NoHelper)]
    [InlineData(12, ItemUseHelper.NoHelper)]
    [InlineData(30, ItemUseHelper.NoHelper)]
    [InlineData(35, ItemUseHelper.NoHelper)]
    [InlineData(0, ItemUseHelper.NoHelper)]
    public void TypesMapToTheServerHelperTable(int type, ItemUseHelper helper)
    {
        Assert.Equal(helper, ItemUseRules.HelperOf(type));
    }

    [Fact]
    public void AnItemWithoutTheUsableBitIsRefused()
    {
        Assert.Equal(ItemUseRefusal.NotUsable, ItemUseRules.Check(Potion(200, 0) with { Options = 0 }, 10));
    }

    [Fact]
    public void ALevelBelowMinLevelIsRefused()
    {
        Assert.Equal(ItemUseRefusal.BelowLevel, ItemUseRules.Check(Potion(200, 0), 2));
        Assert.Null(ItemUseRules.Check(Potion(200, 0), 3));
    }

    [Fact]
    public void AnHpPotionAtFullHpIsNotNeeded()
    {
        Assert.True(ItemUseRules.PotionNotNeeded(Potion(200, 0), 100, 100, 0, 50));
        Assert.False(ItemUseRules.PotionNotNeeded(Potion(200, 0), 99, 100, 0, 50));
    }

    [Fact]
    public void AMixedPotionIsRefusedWhenEitherColumnIsAlreadyFull()
    {
        Assert.True(ItemUseRules.PotionNotNeeded(Potion(200, 100), 10, 100, 50, 50));
        Assert.True(ItemUseRules.PotionNotNeeded(Potion(200, 100), 100, 100, 10, 50));
        Assert.False(ItemUseRules.PotionNotNeeded(Potion(200, 100), 10, 100, 10, 50));
    }

    [Fact]
    public void MonsterCardsAreCardTypeSubTypes18To27()
    {
        Assert.True(ItemUseRules.IsMonsterCard(new ItemParamEntry { Id = 22002, Type = 11, SubType = 18 }));
        Assert.True(ItemUseRules.IsMonsterCard(new ItemParamEntry { Id = 22003, Type = 11, SubType = 27 }));
        Assert.False(ItemUseRules.IsMonsterCard(new ItemParamEntry { Id = 22004, Type = 11, SubType = 33 }));
        Assert.False(ItemUseRules.IsMonsterCard(new ItemParamEntry { Id = 22005, Type = 15, SubType = 18 }));
    }
}
