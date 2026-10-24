using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ElementPowerTests
{
    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_Element", Level = 1, Columns = columns };

    [Fact]
    public void MagicShapesAddTheirElementSlotTimesTheirStep()
    {
        var row = Row(new() { ["AP"] = "1000" });
        var attacker = new SkillAttacker(100, 50, 200, 30, 50, ElementBonus: 3 * 40);

        Assert.Equal(1000 + 120, SkillHitShapes.Power(SkillPowerRule.MagicPower, attacker, default, 0f, row, null));
        Assert.Equal(ElementSlots.Dark, SkillHitShapes.Find(SkillHitShapes.Scythe)!.ElementSlot);
        Assert.Equal(SkillHitShapes.ScytheElementStep, SkillHitShapes.Find(SkillHitShapes.Scythe)!.ElementStep);
        Assert.Equal(ElementSlots.Earth, SkillAreaShapes.Find(1184)!.ElementSlot);
    }

    [Fact]
    public void FlamingFistTurnsMagicWithFireAttack()
    {
        var attacker = new SkillAttacker(500, 50, 200, 30, 50, ElementBonus: 20);

        Assert.Equal((int)(500 * (double)((float)(20 / 100.0) + 1.5f)), SkillHitShapes.Power(SkillPowerRule.AttackTimesFireRatio, attacker, default, 1.5f, null, null));
        Assert.Equal((int)(500 * (double)1.5f), SkillHitShapes.Power(SkillPowerRule.AttackTimesFireRatio, attacker with { ElementBonus = 0 }, default, 1.5f, null, null));
        var shape = SkillHitShapes.Find(SkillHitShapes.FlamingFist)!;
        var row = Row(new() { ["Ratio"] = "1.5", ["ApplyRatio"] = "5" });
        var hit = SkillHitShapes.Resolve(shape, row, null, attacker, new SkillTarget(0, 0, 0, 0, 1000, 1, 0), new Random(1));
        Assert.Equal(SkillDamage.MagicType, hit.Type);
    }
}
