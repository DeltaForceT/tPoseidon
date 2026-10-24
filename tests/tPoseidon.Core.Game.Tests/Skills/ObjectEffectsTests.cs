using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ObjectEffectsTests
{
    [Fact]
    public void IceCrystalLowersElectricResistByHalfTheOwnerMagicTimesRatioCappedAtNinetyFive()
    {
        Assert.Equal((int)(100 * (double)0.45f / 2.0), ObjectEffects.IceResistDrop(100, 0.45f));
        Assert.Equal(95, ObjectEffects.IceResistDrop(1000, 0.8f));
    }

    [Fact]
    public void ElectricFieldTurnsManaIntoMagicAttack()
    {
        Assert.Equal(-5, ObjectEffects.ElectricFieldLoss(600));
        Assert.Equal(0, ObjectEffects.ElectricFieldLoss(119));
    }

    [Fact]
    public void PoisonsFollowTheirPowerFormulas()
    {
        Assert.Equal((int)(900 * (double)0.5f), ObjectEffects.WeightPower(900, 0.5f));
        Assert.Equal((int)(1000 * (double)0.13f), ObjectEffects.PoisonTickDamage(1000, 0.13f));
        Assert.Equal(0, ObjectEffects.PoisonTickDamage(0, 0.13f));
        Assert.Equal((int)(120 + 30 * 16.0 * 1.5f), ObjectEffects.PoisonAssaultPower(120, 30, 1.5f));
        Assert.Equal((int)(0.03f * ((120 + 30 * 16.0) * 2.5f)), ObjectEffects.PoisonAssaultTick(120, 30, 2.5f, 0.03f));
    }

    [Fact]
    public void ThrownWeaponPowerUsesAttackForKnivesAndHalfWeightForCards()
    {
        var knife = new ItemParamEntry { Id = 1, Type = ThrowSkills.ThrowItemType, Ap = 80, Weight = 10 };
        var card = new ItemParamEntry { Id = 2, Type = ThrowSkills.CardItemType, SubType = ThrowSkills.CardSubType, Weight = 1 };

        Assert.Equal((int)(80 * 1.5f), ObjectEffects.ThrownWeaponPower(knife, 1.5f));
        Assert.Equal(1, ObjectEffects.ThrownWeaponPower(card, 1f));
    }

    [Fact]
    public void FullHouseHitsWithAttackAvoidAndLuck()
    {
        Assert.Equal((int)((300 + 8 * 40 + 8 * 20) * (double)1.6f), ObjectEffects.FullHousePower(300, 40, 20, 1.6f));
        Assert.Equal((int)((1000 - 400) * (double)0.1f), ObjectEffects.FullHouseReduced(1000, 400));
    }

    [Fact]
    public void SharpClawModeFollowsTheHitKind()
    {
        Assert.Equal(SkillEffectMode.Failed, ObjectEffects.SharpClawMode(SkillDamage.KindMiss, SkillEffectMode.Applied));
        Assert.Equal(SkillEffectMode.Resisted, ObjectEffects.SharpClawMode(IncomingHits.KindAbsorbed, SkillEffectMode.Applied));
        Assert.Equal(SkillEffectMode.Applied, ObjectEffects.SharpClawMode(SkillDamage.KindCritical, SkillEffectMode.Applied));
        Assert.Equal(SkillEffectMode.Failed, ObjectEffects.SharpClawMode(SkillDamage.KindHit, SkillEffectMode.Failed));
    }

    [Fact]
    public void PoisonStopsAndFullHouseStunsOnTheSameHitKinds()
    {
        Assert.True(ObjectEffects.StopsPoison(SkillDamage.KindHit, 0));
        Assert.True(ObjectEffects.StopsPoison(IncomingHits.KindBlockedByGuard, 50));
        Assert.False(ObjectEffects.StopsPoison(SkillDamage.KindCritical, 50));
        Assert.False(ObjectEffects.StunsAfterFullHouse(IncomingHits.KindGuarded));
        Assert.True(ObjectEffects.StunsAfterFullHouse(SkillDamage.KindHit));
    }

    [Fact]
    public void KindsAreRegisteredWithTheirServerShapes()
    {
        Assert.True(SkillEffectKinds.Find(ObjectEffects.IceCrystal) is { StrikesOnStart: true, Holds: true, ResistDropByOwnerMagic: true, Tail: SkillEffectTail.ModeThenTarget });
        Assert.True(SkillEffectKinds.Find(ObjectEffects.SwampField) is { FieldChild: ObjectEffects.SwampFieldDo, StrikesOnStart: true });
        Assert.True(SkillEffectKinds.Find(ObjectEffects.SettleDownDo) is { TailFloatInverted: true, Tail: SkillEffectTail.RadiusPercent });
        Assert.True(SkillEffectKinds.Find(ObjectEffects.DarkUp) is { SpreadsAs: ObjectEffects.DarkUpDo, SpreadLimit: 0 });
        Assert.True(SkillEffectKinds.Find(ObjectEffects.SharpClaw) is { Periodic: SkillPeriodicHit.StoredBurn, StrikeType: SkillDamage.PhysicalType });
        Assert.True(SkillEffectKinds.Find(ObjectEffects.PoisonAssaultDo) is { Periodic: SkillPeriodicHit.FixedPoison, TickColumn: ObjectEffects.DamagePerSecColumn });
        Assert.True(ObjectHits.IsSelf(ObjectEffects.FullHouse));
        Assert.True(ObjectHits.IsTargeted(ObjectEffects.PoisonAssault));
        Assert.True(GroundSkills.IsGround(ObjectEffects.DeadlyFunk));
    }
}
