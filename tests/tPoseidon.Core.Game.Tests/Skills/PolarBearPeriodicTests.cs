using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearPeriodicTests
{
    private static SkillTableRow Row(params (string Column, string Value)[] columns) => new()
    {
        Table = "t",
        Level = 1,
        Columns = columns.ToDictionary(column => column.Column, column => column.Value),
    };

    private static SkillEffect Effect(int actionId, int durationMs, int tickMs) => new()
    {
        ObjectId = 1,
        Kind = SkillEffectKinds.Find(actionId)!,
        SkillId = 4805,
        EsClass = 0,
        CasterEntityId = 1,
        OwnerEntityId = 2,
        Amount = 0.2f,
        DurationMs = durationMs,
        TickMs = tickMs,
        Mode = SkillEffectMode.Applied,
    };

    [Fact]
    public void TicksFallDueEveryIntervalUpToTheDuration()
    {
        var mole = Effect(1537, 6000, 3000);

        mole.Tick(2999);
        Assert.Equal(0, mole.TakeDueTicks());
        mole.Tick(1);
        Assert.Equal(1, mole.TakeDueTicks());
        mole.Tick(5000);
        Assert.Equal(1, mole.TakeDueTicks());
        Assert.Equal(0, mole.TakeDueTicks());
    }

    [Fact]
    public void MoleHammerTicksAttackMinusThreeQuartersOfDefenceAfterResist()
    {
        var action = Row(("Ratio", "0.350000"));

        Assert.Equal((int)Math.Truncate((1000 * (double)0.35f - 0.75 * 100) * 0.8), PolarBearPeriodic.MoleHammerDamage(action, 1000, 100, 20));
        Assert.Equal(0, PolarBearPeriodic.MoleHammerDamage(action, 100, 1000, 0));
        Assert.Equal(SkillPeriodicHit.DamageOverTime, SkillEffectKinds.Find(1537)!.Periodic);
    }

    [Fact]
    public void HammerWheelHitsUpToKillNumAroundTheBear()
    {
        var action = Row(("Ratio", "2.000000"), ("KillNum", "3"), ("Radius", "115"), ("DaRatio", "0.004000"));
        var candidates = Enumerable.Range(0, 6).Select(i => new AreaCandidate((ushort)(100 + i), 100 + i * 10, 100, false, 0, 0));

        var victims = SkillAreaShapes.Collect(SkillAreaShapes.HammerWheelTick, action, 1, (100, 100), null, candidates);

        Assert.Equal(3, victims.Count);
        Assert.Equal(SkillPeriodicHit.AreaAroundOwner, SkillEffectKinds.Find(1547)!.Periodic);
    }
}
