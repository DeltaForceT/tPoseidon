using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ElementSlotsTests
{
    [Fact]
    public void ElementCodesMapToTheServerResistSlots()
    {
        Assert.Equal(0x14, ElementSlots.ResistSlotOf(SkillActions.NoElement));
        Assert.Equal(0x0F, ElementSlots.ResistSlotOf(SkillActions.ElectricElement));
        Assert.Equal(0x12, ElementSlots.ResistSlotOf(SkillActions.NoPropertyElement));
        Assert.Equal(ElementSlots.None, ElementSlots.ResistSlotOf(0x200));
        Assert.Equal(ElementSlots.Earth, ElementSlots.AttackSlotOf(SkillActions.EarthElement));
        Assert.Equal(ElementSlots.Dark, ElementSlots.AttackSlotOf(SkillActions.DarkElement));
    }

    [Fact]
    public void ResistTotalsAreCappedAtSeventyButAttackSlotsAreNot()
    {
        Assert.Equal(ElementSlots.ResistCap, ElementSlots.Total(0x0F, 60, 30));
        Assert.Equal(90, ElementSlots.Total(ElementSlots.Fire, 60, 30));
    }

    [Fact]
    public void ElementUpBoostIsTheRatioPercentOfTheCurrentTotal()
    {
        Assert.Equal(15, ElementSlots.Boost(100, 15f));
        Assert.Equal((int)(37 * (double)7f / 100.0), ElementSlots.Boost(37, 7f));
    }
}
