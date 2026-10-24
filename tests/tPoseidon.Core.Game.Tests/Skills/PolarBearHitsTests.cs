using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearHitsTests
{
    private static SkillTableRow Row(string table, params (string Column, string Value)[] columns) => new()
    {
        Table = table,
        Level = 1,
        Columns = columns.ToDictionary(column => column.Column, column => column.Value),
    };

    private static readonly SkillTableRow DoubleHammer = Row("ESAction_DoubleHammer", ("Ratio", "1.300000"), ("ApplyRatio", "0.700000"));
    private static readonly SkillTableRow OverSwing = Row("ESAction_OverSwing", ("Ratio", "1.400000"), ("AdRatio", "1.400000"), ("ApplyRatio", "1.000000"));
    private static readonly SkillAttacker Bear = new(200, 50, 0, 0, 20, Da: 30);

    [Fact]
    public void DoubleHammerHitsTwiceWithAttackTimesRatio()
    {
        Assert.Equal(2, PolarBearHits.CountOf(PolarBearHits.DoubleHammer));
        Assert.Equal(259, PolarBearHits.PowerOf(PolarBearHits.DoubleHammer, DoubleHammer, Bear, 0));
        Assert.Equal(259, PolarBearHits.PowerOf(PolarBearHits.DoubleHammer, DoubleHammer, Bear, 1));
    }

    [Fact]
    public void OverSwingAddsDigTimesRatioForEachLaterHit()
    {
        Assert.Equal(3, PolarBearHits.CountOf(PolarBearHits.OverSwing));
        Assert.Equal(279, PolarBearHits.PowerOf(PolarBearHits.OverSwing, OverSwing, Bear, 0));
        Assert.Equal(321, PolarBearHits.PowerOf(PolarBearHits.OverSwing, OverSwing, Bear, 1));
        Assert.Equal(363, PolarBearHits.PowerOf(PolarBearHits.OverSwing, OverSwing, Bear, 2));
    }

    [Fact]
    public void OnlyLandedHitsCountTowardsTheSum()
    {
        var outcome = new PolarBearOutcome(new[]
        {
            new SkillHit(100, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0),
            new SkillHit(0, SkillDamage.PhysicalType, SkillDamage.KindMiss, 0, 0),
            new SkillHit(50, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0),
        });

        Assert.Equal(150, outcome.Sum);
        Assert.Equal(2, outcome.Landed);
        Assert.True(SkillActions.IsBuilt(PolarBearHits.OverSwing));
    }
}
