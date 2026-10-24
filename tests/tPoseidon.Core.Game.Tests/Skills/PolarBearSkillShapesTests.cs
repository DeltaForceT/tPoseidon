using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearSkillShapesTests
{
    private static readonly SkillTableRow EnergeticHammer = new()
    {
        Table = "ESAction_HammerAp",
        Level = 1,
        Columns = new Dictionary<string, string> { ["Ratio"] = "2.000000", ["ApplyRatio"] = "0.800000", ["addAP"] = "140", ["Comment"] = "150" },
    };

    private static readonly SkillTableRow FlyHammerLevelOne = new()
    {
        Table = "ESAction_FlyHammer",
        Level = 1,
        Columns = new Dictionary<string, string> { ["Ratio"] = "7.500000", ["HPRatio"] = "0.220000", ["ApplyRatio"] = "1.000000" },
    };

    [Fact]
    public void FlyHammerAddsTheWeaponsDigAndPartOfCurrentLife()
    {
        var shape = SkillHitShapes.Find(SkillHitShapes.FlyHammer)!;
        var bear = new SkillAttacker(300, 0, 0, 0, 40, Da: 90, Hp: 1000, WeaponDa: 12);

        Assert.Equal(SkillPowerRule.AttackWeaponDigAndLife, shape.Power);
        Assert.Equal(SkillTail.TargetOnly, shape.Tail);
        Assert.Equal(
            (int)Math.Truncate(300 + 12 * (double)7.5f + 1000 * (double)0.22f),
            SkillHitShapes.Power(shape.Power, bear, default, 7.5f, FlyHammerLevelOne, null));
    }

    [Fact]
    public void EnergeticHammerIsBuiltWithThePunchFormula()
    {
        var shape = SkillHitShapes.Find(SkillHitShapes.HammerAp)!;

        Assert.Equal(SkillPowerRule.AttackLevelAndFlat, shape.Power);
        Assert.Equal(SkillTail.TargetOnly, shape.Tail);
        Assert.Equal(100 + 10 * 2 + 140, SkillHitShapes.Power(shape.Power, new SkillAttacker(100, 0, 0, 0, 10), default, 2f, EnergeticHammer, null));
        Assert.Equal(100 + 30 * 2 + 140, SkillHitShapes.Power(shape.Power, new SkillAttacker(100, 0, 0, 0, 45), default, 2f, EnergeticHammer, null));
    }
}
