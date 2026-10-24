using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class KnockSkillsTests
{
    [Fact]
    public void DestinationLiesOnTheCasterVictimRayAtRadius()
    {
        Assert.Equal((150, 0), KnockSkills.Destination((0, 0), (40, 0), 150));
        Assert.Equal((100, 200), KnockSkills.Destination((100, 100), (100, 120), 100));
        Assert.Equal((5, 5), KnockSkills.Destination((5, 5), (5, 5), 100));
    }

    [Fact]
    public void WalkStopsBeforeTheFirstBlockedPoint()
    {
        Assert.Equal((150, 0), KnockSkills.Walk((40, 0), (150, 0), (_, _) => false));
        Assert.Equal((95, 0), KnockSkills.Walk((40, 0), (150, 0), (x, _) => x >= 100));
        Assert.Equal((40, 0), KnockSkills.Walk((40, 0), (150, 0), (_, _) => true));
    }

    [Fact]
    public void PowerGrowsWithThePushedDistanceAndCasterLife()
    {
        Assert.Equal(110, KnockSkills.PushedDistance((40, 0), (150, 0)));
        Assert.Equal((int)((110 + 40) * 2000 * (double)1.5f / 512.0), KnockSkills.Power(110, 2000, 1.5f));
    }

    [Fact]
    public void KnockObjectsMoveTheirOwnerLater()
    {
        Assert.True(SkillEffectKinds.Find(KnockSkills.ChargingHit) is { Periodic: SkillPeriodicHit.KnockMove, FixedDurationMs: KnockSkills.ChargingMoveMs, Tail: SkillEffectTail.ModeThenPoint });
        Assert.True(SkillEffectKinds.Find(KnockSkills.PowerChargingDo) is { FixedDurationMs: KnockSkills.PowerChargingMoveMs });
        Assert.True(SkillEffectKinds.Find(KnockSkills.MassiveAttack) is { HeaderClass: 1 });
        Assert.Equal(KnockSkills.PowerChargingDo, KnockSkills.ObjectOf(KnockSkills.PowerCharging));
        Assert.True(ObjectHits.IsSelf(KnockSkills.ImpellingRage));
    }
}
