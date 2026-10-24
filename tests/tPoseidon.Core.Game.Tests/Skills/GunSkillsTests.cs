using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class GunSkillsTests
{
    private static readonly SkillAttacker Gunner = new(0, 200, 0, 150, 60);

    [Fact]
    public void AGunHitUsesAccuracyWeaponPowerAndTheTargetsAvoid()
    {
        var target = new SkillTarget(0, 10, 0, 0, 1_000_000, 1, 0);
        var hits = Enumerable.Range(0, 200)
            .Select(seed => GunSkills.Hit(Gunner, 300, target, targetIsPlayer: false, 1.5f, 5f, 1f, new Random(seed)))
            .ToList();
        var normal = hits.First(hit => hit.Kind == SkillDamage.KindHit);
        var critical = hits.First(hit => hit.Kind == SkillDamage.KindCritical);
        var raw = (200 - 49) * 20.0 + 300 + 20.0;

        Assert.Equal((int)((float)raw * (double)1.5f) - (int)(10 * 30.0), normal.Damage);
        Assert.Equal((int)(raw * 1.2 * 1.5f - 10 * 30.0), critical.Damage);
        Assert.All(hits, hit => Assert.Equal(GunSkills.GunType, hit.Type));
        Assert.All(hits, hit => Assert.Equal(SkillActions.GunElement, hit.Element));
    }

    [Fact]
    public void GunResistAboveOneHundredStopsTheDamage()
    {
        var immune = new SkillTarget(0, 0, 0, 0, 1_000_000, 1, 120);

        Assert.All(
            Enumerable.Range(0, 50).Select(seed => GunSkills.Hit(Gunner, 300, immune, false, 1.5f, 5f, 1f, new Random(seed))),
            hit => Assert.Equal(0, hit.Damage));
    }

    [Fact]
    public void BreakBarrierLowersPhysicalAndGunResistUpToEighty()
    {
        Assert.Equal((int)((60 * (double)0.45f + 20.0) / 3.0), GunSkills.BarrierBreak(60, 0.45f));
        Assert.Equal(80, GunSkills.BarrierBreak(5000, 0.8f));
        var kind = SkillEffectKinds.Find(GunSkills.BreakBarrier)!;
        Assert.True(kind.BreaksBarrier);
        Assert.Contains(1311, kind.StripsActions);
        Assert.True(GunSkills.HitsAround(GunSkills.ShootingSpree));
        Assert.True(SkillActions.IsBuilt(GunSkills.PowerShot));
    }
}
