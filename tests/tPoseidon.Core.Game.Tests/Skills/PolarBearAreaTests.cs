using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearAreaTests
{
    private static readonly SkillTableRow QuakeMaster = new()
    {
        Table = "ESAction_QuakeHammer",
        Level = 11,
        Columns = new Dictionary<string, string>
        {
            ["Ratio"] = "3.800000", ["KillNum"] = "5", ["Radius"] = "150", ["ApplyRatio"] = "1.100000", ["DaRatio"] = "0.027000",
        },
    };

    [Fact]
    public void EarthquakeHammerAddsDigTimesItsRatioToTheMultiplier()
    {
        var shape = SkillAreaShapes.Find(SkillAreaShapes.QuakeHammer)!;
        var bear = new SkillAttacker(400, 0, 0, 0, 60, Da: 100);

        Assert.Equal(AreaCentre.Caster, shape.Centre);
        Assert.Equal(AreaTail.None, shape.Tail);
        Assert.Equal(5, SkillAreaShapes.LimitOf(shape, QuakeMaster));
        Assert.Equal((int)Math.Truncate(400 * ((double)3.8f + 100 * (double)0.027f)), SkillAreaShapes.Power(shape, QuakeMaster, bear));
    }

    [Fact]
    public void HighSpeedDropHitsAroundTheCaster()
    {
        var shape = SkillAreaShapes.Find(SkillAreaShapes.HighSpeedDrop)!;

        Assert.Equal(AreaCentre.Caster, shape.Centre);
        Assert.Equal(SkillAreaPower.AttackTimesRatio, shape.Power);
        Assert.True(SkillActions.IsBuilt(SkillAreaShapes.HighSpeedDrop));
    }
}
