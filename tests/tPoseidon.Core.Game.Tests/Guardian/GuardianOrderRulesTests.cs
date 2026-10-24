using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianOrderRulesTests
{
    private static GuardianState Guardian(params GuardianSkill[] skills) =>
        new(0x55, 0, 1, "g", 1, 0, 0, 0, 0, 0, 0, 0, new short[12], 0, 0, 0, skills);

    private static readonly GuardianSkill Card = new(4101, GuardianSkillType.Card, 2, 3, 5);
    private static readonly GuardianSkill Ordered = new(4101, GuardianSkillType.Order, 2, 3, 1);

    [Fact]
    public void BusyComesFirstThenTheCardAndListChecks()
    {
        Assert.Equal(GuardianOrderRules.Busy, GuardianOrderRules.Refusal(Guardian(Card), 4101, false, true));
        Assert.Equal(GuardianOrderRules.NotACard, GuardianOrderRules.Refusal(Guardian(), 4101, false, false));
        Assert.Equal(GuardianOrderRules.NotACard, GuardianOrderRules.Refusal(null, 4101, false, false));
        Assert.Equal(GuardianOrderRules.AlreadyListed, GuardianOrderRules.Refusal(Guardian(Card, Ordered), 4101, false, false));
        Assert.Equal(GuardianOrderRules.NotListed, GuardianOrderRules.Refusal(Guardian(Card), 4101, true, false));
        Assert.Null(GuardianOrderRules.Refusal(Guardian(Card), 4101, false, false));
        Assert.Null(GuardianOrderRules.Refusal(Guardian(Card, Ordered), 4101, true, false));
    }

    [Fact]
    public void SlotIsTheLowByteOfTheSecondField()
    {
        Assert.Equal(3, GuardianOrderRules.SlotOf(0x0103));
        Assert.Equal(0, GuardianOrderRules.SlotOf(0));
    }

    [Fact]
    public void RecordCopiesTheCardAndApplyEditsTheOrderList()
    {
        var record = GuardianOrderRules.Record(Guardian(Card), 4101, 4, false);
        Assert.Equal(new GuardianSkill(4101, GuardianSkillType.Order, 2, 3, 4), record);
        var added = GuardianOrderRules.Apply(Guardian(Card), record, false);
        Assert.Contains(record, added.Skills);
        var removedRecord = GuardianOrderRules.Record(added, 4101, 0, true);
        Assert.Equal(4, removedRecord.Slot);
        Assert.DoesNotContain(added.Skills.Where(skill => skill.Type == GuardianSkillType.Order).First(), GuardianOrderRules.Apply(added, removedRecord, true).Skills);
    }
}
