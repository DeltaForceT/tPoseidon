using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SkillAreaShapesTests
{
    private const ushort Caster = 0x0301;

    private static SkillTableRow Row(params (string Column, string Value)[] columns) => new()
    {
        Table = "Area",
        Level = 1,
        Columns = columns.ToDictionary(column => column.Column, column => column.Value),
    };

    private static AreaCandidate Monster(ushort id, int x, int y, int type = 1, ushort owner = 0, bool dead = false) =>
        new(id, x, y, dead, type, owner);

    [Fact]
    public void CasterCentredAreasTakeLivingUnownedMonstersInRangeUpToTheLimit()
    {
        var shape = SkillAreaShapes.Find(1352)!;
        var row = Row(("Radius", "120"), ("KillNum", "2"), ("Ratio", "2.0"), ("ApplyRatio", "0.8"));
        var candidates = new[]
        {
            Monster(1, 110, 100),
            Monster(2, 100, 300),
            Monster(3, 120, 100, dead: true),
            Monster(4, 130, 100, owner: 0x0999),
            Monster(5, 140, 100, type: SkillAreaShapes.UntouchableMonsterType),
            Monster(6, 150, 100),
            Monster(7, 160, 100),
        };

        var victims = SkillAreaShapes.Collect(shape, row, Caster, (100, 100), null, candidates);

        Assert.Equal(new ushort[] { 1, 6 }, victims);
    }

    [Fact]
    public void TempestPutsTheSelectedTargetFirstWhenTheScanMissedIt()
    {
        var shape = SkillAreaShapes.Find(1325)!;
        var row = Row(("Radius", "110"), ("KillNum", "1"));
        var target = Monster(9, 500, 500);

        var victims = SkillAreaShapes.Collect(shape, row, Caster, (100, 100), target, new[] { Monster(1, 110, 100), target });

        Assert.Equal(new ushort[] { 9 }, victims);
    }

    [Fact]
    public void ConeSkillsKeepTheTargetFirstAndOnlyTakeMonstersInsideTheCone()
    {
        var shape = SkillAreaShapes.Find(1244)!;
        var row = Row(("Radius", "200"), ("Angle", "20.0"));
        var target = Monster(9, 100, 200);
        var behind = Monster(1, 100, 260);
        var aside = Monster(2, 240, 200);

        var victims = SkillAreaShapes.Collect(shape, row, Caster, (100, 100), target, new[] { target, behind, aside });

        Assert.Equal(new ushort[] { 9, 1 }, victims);
    }

    [Fact]
    public void BearingFollowsTheServerFormula()
    {
        Assert.Equal(237.29578f, SkillAreaShapes.Bearing(0, 0, 0, 10), 3);
        Assert.Equal(180f, SkillAreaShapes.Bearing(0, 0, -10, 0), 3);
        Assert.Equal(0f, SkillAreaShapes.Bearing(0, 0, 10, 0), 3);
    }

    [Fact]
    public void LimitsTakeTheSmallerOfTheCapAndTheRowColumn()
    {
        Assert.Equal(5, SkillAreaShapes.LimitOf(SkillAreaShapes.Find(1310)!, Row()));
        Assert.Equal(5, SkillAreaShapes.LimitOf(SkillAreaShapes.Find(1325)!, Row(("KillNum", "9"))));
        Assert.Equal(3, SkillAreaShapes.LimitOf(SkillAreaShapes.Find(1199)!, Row(("MonNum", "3"))));
    }

    [Fact]
    public void AreaPowerRulesFollowTheServerFormulas()
    {
        var attacker = new SkillAttacker(Ap: 100, Ac: 40, Ma: 60, Lk: 10, Level: 3, Hp: 200, Hv: 5, Dp: 30);
        var row = Row(("Ratio", "2.0"), ("AP", "2100"));

        Assert.Equal(200, SkillAreaShapes.Power(SkillAreaShapes.Find(1310)!, row, attacker));
        Assert.Equal(2100, SkillAreaShapes.Power(SkillAreaShapes.Find(1184)!, row, attacker));
        Assert.Equal(70, SkillAreaShapes.Power(SkillAreaShapes.Find(1199)!, row, attacker));
        Assert.Equal(270, SkillAreaShapes.Power(SkillAreaShapes.Find(1381)!, row, attacker));
    }
}
