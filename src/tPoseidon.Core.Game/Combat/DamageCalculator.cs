using tPoseidon.Core.Common;

namespace tPoseidon.Core.Game.Combat;

public enum DamageKind
{
    PhysicalMelee,
    PhysicalSkill,
    PowerSkill,
    MagicSkill,
    Gun,
    Poison,
}

public enum DamageElement
{
    None,
    Fire,
    Water,
    Air,
    Soil,
    Electricity,
    Light,
    Dark,
    Shadow,
}

public readonly record struct AttackerStats(
    long Ap,
    long Ma,
    long Ac,
    long GunAp);

public readonly record struct DefenderStats(
    long Dp,
    long Md,
    long Hv,
    long CurrentHp,
    int PhysResistPct,
    int MagicResistPct,
    int GunResistPct,
    int FireResistPct,
    int WaterResistPct,
    int AirResistPct,
    int SoilResistPct,
    int ElectricityResistPct,
    int LightResistPct,
    int DarkResistPct,
    int ShadowResistPct);

public readonly record struct DamageInput(
    DamageKind Kind,
    DamageElement Element = DamageElement.None,
    long SkillBaseDamage = 0,
    int AttrPercent = 0,
    bool IsCrit = false,
    bool IsMobAttack = false);

public readonly record struct DamageResult(long Damage, byte HitFlags, uint HitKind);

public sealed class DamageCalculator
{
    private readonly Func<ServerRates> _ratesAccessor;

    public DamageCalculator(ServerRates rates) : this(() => rates) { }

    public DamageCalculator(Func<ServerRates> ratesAccessor)
    {
        ArgumentNullException.ThrowIfNull(ratesAccessor);
        _ratesAccessor = ratesAccessor;
    }

    private ServerRates _rates => _ratesAccessor();

    public DamageResult Compute(AttackerStats attacker, DefenderStats defender, DamageInput input)
    {
        var critApplied = ResolveCritMultiplier(input) > 1d;

        if (input.Kind == DamageKind.Poison)
        {
            return Build(input.SkillBaseDamage, defender, input, critApplied);
        }

        var raw = ComputeRaw(attacker, input);
        var mitigated = Math.Max(1d, raw - MitigationFloor(defender, input));

        if (input.Kind == DamageKind.PhysicalMelee && input.AttrPercent > 0 && input.Element != DamageElement.None)
        {
            var attrPct = input.AttrPercent / 100d;
            var attrLayer = (raw * attrPct) - (defender.Dp * (double)_rates.Combat.PhysFactor * attrPct);
            attrLayer = Math.Max(0d, attrLayer) * (1d - LookupResistance(defender, input.Element) / 100d);
            mitigated = (mitigated * (1d - LookupResistance(defender, DamageElement.None, fallbackPhysical: true) / 100d)) + attrLayer;
            return Build(WireLimits.Saturate(Math.Floor(mitigated)), defender, input, critApplied, alreadyResisted: true);
        }

        return Build(WireLimits.Saturate(Math.Floor(mitigated)), defender, input, critApplied);
    }

    private double ComputeRaw(AttackerStats attacker, DamageInput input)
    {
        var critMult = ResolveCritMultiplier(input);
        return input.Kind switch
        {
            DamageKind.PhysicalMelee => attacker.Ap * critMult,
            DamageKind.PhysicalSkill or DamageKind.PowerSkill => input.SkillBaseDamage * critMult,
            DamageKind.MagicSkill => input.SkillBaseDamage * critMult,
            DamageKind.Gun => Math.Max(0d, 20d * (attacker.Ac - (double)_rates.Combat.GunAccuracyOffset) + attacker.GunAp) * critMult,
            _ => 0d,
        };
    }

    private double ResolveCritMultiplier(DamageInput input)
    {
        if (!input.IsCrit) return 1d;
        if (input.IsMobAttack && !_rates.Combat.MobCanCrit) return 1d;
        return _rates.Combat.CritMultiplier;
    }

    private double MitigationFloor(DefenderStats defender, DamageInput input) => input.Kind switch
    {
        DamageKind.PhysicalMelee or DamageKind.PhysicalSkill or DamageKind.PowerSkill => (double)_rates.Combat.PhysFactor * defender.Dp,
        DamageKind.MagicSkill => (double)_rates.Combat.MagicFactor * defender.Md,
        DamageKind.Gun => (double)_rates.Combat.GunFactor * defender.Hv,
        _ => 0d,
    };

    private DamageResult Build(long preResist, DefenderStats defender, DamageInput input, bool critApplied, bool alreadyResisted = false)
    {
        var resisted = alreadyResisted
            ? preResist
            : WireLimits.Saturate(Math.Floor(preResist * (1d - LookupResistance(defender, input.Element, fallbackPhysical: input.Kind is DamageKind.PhysicalMelee or DamageKind.PhysicalSkill or DamageKind.PowerSkill, fallbackMagical: input.Kind == DamageKind.MagicSkill, fallbackGun: input.Kind == DamageKind.Gun) / 100d)));

        var clamped = Math.Clamp(resisted, 1L, ServerRates.CapsSection.Limit(_rates.Caps.MaxDamagePerHit));
        var (flags, kind) = ResolveHit(input, critApplied);
        return new DamageResult(clamped, flags, kind);
    }

    private static double LookupResistance(
        DefenderStats defender,
        DamageElement element,
        bool fallbackPhysical = false,
        bool fallbackMagical = false,
        bool fallbackGun = false) => element switch
    {
        DamageElement.Fire => defender.FireResistPct,
        DamageElement.Water => defender.WaterResistPct,
        DamageElement.Air => defender.AirResistPct,
        DamageElement.Soil => defender.SoilResistPct,
        DamageElement.Electricity => defender.ElectricityResistPct,
        DamageElement.Light => defender.LightResistPct,
        DamageElement.Dark => defender.DarkResistPct,
        DamageElement.Shadow => defender.ShadowResistPct,
        DamageElement.None when fallbackPhysical => defender.PhysResistPct,
        DamageElement.None when fallbackMagical => defender.MagicResistPct,
        DamageElement.None when fallbackGun => defender.GunResistPct,
        _ => 0,
    };

    private static (byte flags, uint kind) ResolveHit(DamageInput input, bool critApplied)
    {
        if (input.IsMobAttack)
        {
            return (critApplied ? DamageHitFlags.Crit : DamageHitFlags.MobHit, DamageHitKind.MobToPlayer);
        }
        return (critApplied ? DamageHitFlags.Crit : DamageHitFlags.Normal, DamageHitKind.PlayerToMob);
    }
}
