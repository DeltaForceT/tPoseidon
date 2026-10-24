using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ThrowSkillsTests
{
    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_OnePair", Level = 1, Columns = columns };

    [Fact]
    public void OnePairThrowsTwoCardsUnlessLadyLuckSaysOtherwise()
    {
        Assert.Equal(2, ThrowSkills.CardsFor(null, 12345));
        var fixedLuck = Row(new() { ["MinCard"] = "2", ["MaxCard"] = "2" });
        Assert.Equal(2, ThrowSkills.CardsFor(fixedLuck, 30000));
        var rangedLuck = Row(new() { ["MinCard"] = "4", ["MaxCard"] = "6" });
        Assert.Equal(4, ThrowSkills.CardsFor(rangedLuck, 0));
        Assert.Equal(6, ThrowSkills.CardsFor(rangedLuck, 32767));
    }

    [Fact]
    public void LadyLuckRaisesOnePairByCardsOverFifteen()
    {
        var row = Row(new() { ["Ratio"] = "2.200000" });
        var plain = (int)((300 + 8 * 50) * (double)2.2f);

        Assert.Equal(plain, ThrowSkills.OnePairPower(row, 300, 50, 2, ladyLuck: false));
        Assert.Equal((int)(plain * (double)((float)(6 / 15.0) + 1.0f)), ThrowSkills.OnePairPower(row, 300, 50, 6, ladyLuck: true));
    }

    [Fact]
    public void FuriousGalderPaysByBand()
    {
        Assert.Equal(1, ThrowSkills.GalderPay(0));
        Assert.Equal(500, ThrowSkills.GalderPay(0x7FFF * 50 / 100));
        Assert.Equal(1000, ThrowSkills.GalderPay(0x7FFF));
        Assert.Equal((int)(700 * (double)4.5f), ThrowSkills.GalderPower(700, 4.5f));
        Assert.Equal(0, ThrowSkills.GalderPower(700, -1f));
    }

    [Fact]
    public void ThrownItemsUseTheirAttackAndCardsHalfTheirWeight()
    {
        var knife = new ItemParamEntry { Id = 1, Type = ThrowSkills.ThrowItemType, Ap = 120, Weight = 10 };
        var card = new ItemParamEntry { Id = 2, Type = ThrowSkills.CardItemType, SubType = ThrowSkills.CardSubType, Ap = 0, Weight = 30 };

        Assert.Equal((int)((120 * 1f + 40 * 10) * (double)2.6f), ThrowSkills.ThrownPower(knife, 40, 1f, 2.6f));
        Assert.Equal(15, ThrowSkills.ThrownPower(card, 40, 1f, 2.6f));
        Assert.True(ThrowSkills.IsThrowable(card));
    }
}
