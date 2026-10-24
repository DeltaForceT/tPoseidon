using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Game.Tests.Characters;

public sealed class LookChangeRulesTests
{
    private static readonly ushort[] ThirdJobLooks = [2, 2, 10, 10, 20, 20];

    private static Character Hero(byte job2, byte job3) => new()
    {
        CharacterId = 7,
        OwnerAccountId = 1,
        Slot = 0,
        Name = "Hero",
        Species = Species.Buffalo,
        FType = FType.Power,
        Hair = 3,
        Hair2 = 4,
        Hair3 = 5,
        Type2 = job2 == 0 ? (ushort)0 : (ushort)10,
        Job2 = job2,
        Type3 = job3 == 0 ? (ushort)0 : (ushort)20,
        Job3 = job3,
        JobState = 3,
    };

    [Fact]
    public void OpenChecksMoneyJobBagThenDisguiseInThatOrder()
    {
        Assert.Equal(LookChangeRules.NotEnoughMoney, LookChangeRules.OpenRefusal(49999, 1, 9, 0, true));
        Assert.Equal(LookChangeRules.JobTooLow, LookChangeRules.OpenRefusal(50000, 8, 9, 0, true));
        Assert.Equal(LookChangeRules.BagTooSmall, LookChangeRules.OpenRefusal(50000, 9, 9, 8, true));
        Assert.Equal(LookChangeRules.WhileDisguised, LookChangeRules.OpenRefusal(50000, 9, 9, 9, true));
        Assert.Null(LookChangeRules.OpenRefusal(50000, 20, 9, 9, false));
        Assert.True(LookChangeRules.RequestAllowed(50000, 9, 0, 0));
        Assert.False(LookChangeRules.RequestAllowed(50000, 9, 1, 0));
    }

    [Fact]
    public void LooksAreJobTypePairsPerTier()
    {
        Assert.Equal(ThirdJobLooks, LookChangeRules.Looks(Hero(10, 20)));
    }

    [Fact]
    public void MakeUpNeedsTheTierToBeHeld()
    {
        var hero = Hero(10, 0);
        Assert.Equal(new MakeUp(2, 2, 3), LookChangeRules.MakeUpOf(hero, 1));
        Assert.Equal(new MakeUp(10, 10, 4), LookChangeRules.MakeUpOf(hero, 2));
        Assert.Null(LookChangeRules.MakeUpOf(hero, 3));
        Assert.Null(LookChangeRules.MakeUpOf(hero, 4));
        Assert.Equal(new MakeUp(20, 20, 5), LookChangeRules.MakeUpOf(Hero(10, 20), 3));
    }
}
