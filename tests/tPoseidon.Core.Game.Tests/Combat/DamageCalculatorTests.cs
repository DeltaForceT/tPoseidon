using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Tests.Combat;

public class DamageCalculatorTests
{
    private static DamageCalculator NewCalc() => new(new ServerRates());

    private static DefenderStats DefenderWith(int dp = 0, int md = 0, int hv = 0, int currentHp = 1000,
        int phys = 0, int magic = 0, int gun = 0, int fire = 0, int water = 0, int air = 0,
        int soil = 0, int elec = 0, int light = 0, int dark = 0, int shadow = 0)
        => new(Dp: dp, Md: md, Hv: hv, CurrentHp: currentHp,
            PhysResistPct: phys, MagicResistPct: magic, GunResistPct: gun,
            FireResistPct: fire, WaterResistPct: water, AirResistPct: air,
            SoilResistPct: soil, ElectricityResistPct: elec,
            LightResistPct: light, DarkResistPct: dark, ShadowResistPct: shadow);

    [Fact]
    public void PhysicalMelee_NoMitigation_ReturnsRawAp()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 0);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));
        Assert.Equal(100, result.Damage);
        Assert.Equal(DamageHitFlags.Normal, result.HitFlags);
        Assert.Equal(DamageHitKind.PlayerToMob, result.HitKind);
    }

    [Fact]
    public void PhysicalMelee_AppliesPhysFactor_0_75_AgainstDp()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 40);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));
        Assert.Equal(70, result.Damage);
    }

    [Fact]
    public void PhysicalMelee_AppliesPhysResistance()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 0, phys: 50);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));
        Assert.Equal(50, result.Damage);
    }

    [Fact]
    public void PhysicalMelee_Crit_Multiplies_BeforeMitigation()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 40);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee, IsCrit: true));
        Assert.Equal(120, result.Damage);
        Assert.Equal(DamageHitFlags.Crit, result.HitFlags);
        Assert.Equal(DamageHitKind.PlayerToMob, result.HitKind);
    }

    [Fact]
    public void PhysicalMelee_LethalCrit_RetainsCritFlag()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 0, currentHp: 50);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee, IsCrit: true));
        Assert.Equal(150, result.Damage);
        Assert.Equal(DamageHitFlags.Crit, result.HitFlags);
        Assert.Equal(DamageHitKind.PlayerToMob, result.HitKind);
    }

    [Fact]
    public void PowerSkill_AppliesPhysFactor_AgainstDp_PerSkillDamage()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 40, fire: 0);
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.PowerSkill, Element: DamageElement.Fire, SkillBaseDamage: 200));
        Assert.Equal(170, result.Damage);
    }

    [Fact]
    public void PowerSkill_FireResistance_ReducesDamage()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 0, fire: 50);
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.PowerSkill, Element: DamageElement.Fire, SkillBaseDamage: 200));
        Assert.Equal(100, result.Damage);
    }

    [Fact]
    public void MagicSkill_AppliesMagicFactor_0_9_AgainstMd()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(md: 100);
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.MagicSkill, SkillBaseDamage: 200));
        Assert.Equal(110, result.Damage);
    }

    [Fact]
    public void MagicSkill_DarkElement_AppliesDarkResistance()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(md: 0, dark: 25);
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.MagicSkill, Element: DamageElement.Dark, SkillBaseDamage: 200));
        Assert.Equal(150, result.Damage);
    }

    [Fact]
    public void Gun_NormalAttack_AppliesAccuracyOffsetAndGunAp()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 100, GunAp: 50);
        var defender = DefenderWith(hv: 0);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.Gun));
        Assert.Equal(1090, result.Damage);
    }

    [Fact]
    public void Gun_AppliesGunFactor_30_AgainstHv()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 100, GunAp: 50);
        var defender = DefenderWith(hv: 10);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.Gun));
        Assert.Equal(790, result.Damage);
    }

    [Fact]
    public void Gun_LowAccuracy_FloorsToOne()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 10, GunAp: 0);
        var defender = DefenderWith(hv: 0);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.Gun));
        Assert.Equal(1, result.Damage);
    }

    [Fact]
    public void Poison_BypassesAllMitigation()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 999, md: 999, phys: 90, magic: 90);
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.Poison, SkillBaseDamage: 75));
        Assert.Equal(75, result.Damage);
    }

    [Fact]
    public void DamageFloor_NeverBelowOne()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 1, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith(dp: 1000);
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));
        Assert.Equal(1, result.Damage);
    }

    [Fact]
    public void DamageCap_RespectsMaxDamagePerHit()
    {
        var rates = new ServerRates();
        var calc = new DamageCalculator(rates);
        var attacker = new AttackerStats(Ap: 100000, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith();
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee));
        Assert.True(result.Damage <= rates.Caps.MaxDamagePerHit);
        Assert.True(result.Damage <= ushort.MaxValue);
    }

    [Fact]
    public void MobAttack_NormalHit_SetsMobToPlayerKind()
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 50, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith();
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.PhysicalMelee, IsMobAttack: true));
        Assert.Equal(DamageHitFlags.MobHit, result.HitFlags);
        Assert.Equal(DamageHitKind.MobToPlayer, result.HitKind);
    }

    [Fact]
    public void MobAttack_CritIgnored_WhenMobCanCritFalse()
    {
        var rates = new ServerRates();
        Assert.False(rates.Combat.MobCanCrit);
        var calc = new DamageCalculator(rates);
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith();
        var result = calc.Compute(attacker, defender, new DamageInput(
            DamageKind.PhysicalMelee, IsCrit: true, IsMobAttack: true));
        Assert.Equal(100, result.Damage);
        Assert.Equal(DamageHitFlags.MobHit, result.HitFlags);
        Assert.Equal(DamageHitKind.MobToPlayer, result.HitKind);
    }

    [Fact]
    public void WireSample_Lv3Ura_OnTorobbie_Reproduces32Damage()
    {
        var calc = NewCalc();
        var ura = new AttackerStats(Ap: 35, Ma: 0, Ac: 0, GunAp: 0);
        var torobbie = DefenderWith(dp: 4, currentHp: 60);
        var result = calc.Compute(ura, torobbie, new DamageInput(DamageKind.PhysicalMelee));
        Assert.Equal(32, result.Damage);
    }

    [Fact]
    public void WireSample_Lv3Ura_CritOnTorobbie_ReproducesAround48()
    {
        var calc = NewCalc();
        var ura = new AttackerStats(Ap: 35, Ma: 0, Ac: 0, GunAp: 0);
        var torobbie = DefenderWith(dp: 4, currentHp: 60);
        var result = calc.Compute(ura, torobbie, new DamageInput(DamageKind.PhysicalMelee, IsCrit: true));
        Assert.InRange(result.Damage, 48, 50);
    }

    [Fact]
    public void CombatSection_DefaultsMatchSpec()
    {
        var rates = new ServerRates();
        Assert.Equal(1.5f, rates.Combat.CritMultiplier);
        Assert.Equal(0.75f, rates.Combat.PhysFactor);
        Assert.Equal(0.9f, rates.Combat.MagicFactor);
        Assert.Equal(30f, rates.Combat.GunFactor);
        Assert.Equal(48f, rates.Combat.GunAccuracyOffset);
        Assert.False(rates.Combat.MobCanCrit);
    }

    [Fact]
    public void ConfigurableCritMultiplier_OverridesDefault()
    {
        var rates = new ServerRates { Combat = new ServerRates.CombatSection { CritMultiplier = 2.0f } };
        var calc = new DamageCalculator(rates);
        var attacker = new AttackerStats(Ap: 100, Ma: 0, Ac: 0, GunAp: 0);
        var defender = DefenderWith();
        var result = calc.Compute(attacker, defender, new DamageInput(DamageKind.PhysicalMelee, IsCrit: true));
        Assert.Equal(200, result.Damage);
    }

    [Theory]
    [InlineData(DamageElement.Fire)]
    [InlineData(DamageElement.Water)]
    [InlineData(DamageElement.Air)]
    [InlineData(DamageElement.Soil)]
    [InlineData(DamageElement.Electricity)]
    [InlineData(DamageElement.Light)]
    [InlineData(DamageElement.Dark)]
    [InlineData(DamageElement.Shadow)]
    public void MagicSkill_EveryElement_AppliesMatchingResistance(DamageElement element)
    {
        var calc = NewCalc();
        var attacker = new AttackerStats(Ap: 0, Ma: 0, Ac: 0, GunAp: 0);
        var fullResist = element switch
        {
            DamageElement.Fire => DefenderWith(fire: 100),
            DamageElement.Water => DefenderWith(water: 100),
            DamageElement.Air => DefenderWith(air: 100),
            DamageElement.Soil => DefenderWith(soil: 100),
            DamageElement.Electricity => DefenderWith(elec: 100),
            DamageElement.Light => DefenderWith(light: 100),
            DamageElement.Dark => DefenderWith(dark: 100),
            DamageElement.Shadow => DefenderWith(shadow: 100),
            _ => DefenderWith(),
        };
        var result = calc.Compute(attacker, fullResist, new DamageInput(
            DamageKind.MagicSkill, Element: element, SkillBaseDamage: 200));
        Assert.Equal(1, result.Damage);
    }
}
