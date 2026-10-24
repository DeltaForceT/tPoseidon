using tPoseidon.Core.Game.PlayerShop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.PlayerShop;

public sealed class PlayerShopSearchRulesTests
{
    private static ItemParamEntry Item(int type, int subType = 0, int minLevel = 10, string name = "Red Potion") =>
        new() { Id = 1, Type = type, SubType = subType, MinLevel = minLevel, NameEng = name };

    [Theory]
    [InlineData(2, 2, PlayerShopSearchRules.WeaponCategory)]
    [InlineData(2, 54, PlayerShopSearchRules.GunCategory)]
    [InlineData(3, 3, PlayerShopSearchRules.HatCategory)]
    [InlineData(8, 8, PlayerShopSearchRules.ShieldCategory)]
    [InlineData(34, 0, PlayerShopSearchRules.CapeCategory)]
    [InlineData(7, 0, PlayerShopSearchRules.AccessoryCategory)]
    [InlineData(4, 0, PlayerShopSearchRules.ArmourCategory)]
    [InlineData(5, 0, PlayerShopSearchRules.ArmourCategory)]
    [InlineData(13, 0, PlayerShopSearchRules.PetCategory)]
    [InlineData(24, 48, PlayerShopSearchRules.HeadCategory)]
    [InlineData(25, 49, PlayerShopSearchRules.FaceCategory)]
    [InlineData(6, 0, PlayerShopSearchRules.DrillCategory)]
    [InlineData(19, 0, PlayerShopSearchRules.FashionCategory)]
    [InlineData(10, 0, PlayerShopSearchRules.PotionCategory)]
    [InlineData(11, 18, PlayerShopSearchRules.CardCategory)]
    [InlineData(14, 0, PlayerShopSearchRules.OtherCategory)]
    public void CategoriesFollowTheItemType(int type, int subType, byte category)
    {
        Assert.Equal(category, PlayerShopSearchRules.CategoryOf(Item(type, subType)));
    }

    [Fact]
    public void MatchesCategoryLevelRangeAndName()
    {
        var potion = Item(10, minLevel: 10);
        Assert.True(PlayerShopSearchRules.Matches(potion, new PlayerShopSearchQuery(12, 0, 1, 20, "potion")));
        Assert.False(PlayerShopSearchRules.Matches(potion, new PlayerShopSearchQuery(12, 0, 11, 20, string.Empty)));
        Assert.False(PlayerShopSearchRules.Matches(potion, new PlayerShopSearchQuery(12, 0, 1, 20, "sword")));
        Assert.False(PlayerShopSearchRules.Matches(potion, new PlayerShopSearchQuery(13, 0, 1, 20, string.Empty)));
    }

    [Fact]
    public void ResultsComeInPagesOfTen()
    {
        var rows = Enumerable.Range(1, 23).Select(n => new PlayerShopSearchRow((ushort)n, 1, "seller", 1, 100, (ulong)n, 5, 10, 5));
        var result = new PlayerShopSearchResult(rows);
        Assert.Equal(10, result.TakePage().Count);
        Assert.Equal(10, result.TakePage().Count);
        Assert.Equal(3, result.TakePage().Count);
        Assert.Empty(result.TakePage());
        Assert.True(PlayerShopSearchRules.PriceHolds(10, new PlayerShopRemoteBuy(1, 1, 3, 30)));
        Assert.False(PlayerShopSearchRules.PriceHolds(11, new PlayerShopRemoteBuy(1, 1, 3, 30)));
    }
}
