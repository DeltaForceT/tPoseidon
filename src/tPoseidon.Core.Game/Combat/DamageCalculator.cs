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
    int Ap,
    int Ma,
    int Ac,
    int GunAp);

public readonly record struct DefenderStats(
    int Dp,
    int Md,
    int Hv,
    int CurrentHp,
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
    int SkillBaseDamage = 0,
    int AttrPercent = 0,
    bool IsCrit = false,
    bool IsMobAttack = false);

public readonly record struct DamageResult(ushort Damage, byte HitFlags, uint HitKind);

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
        var critApplied = ResolveCritMultiplier(input) > 1f;

        if (input.Kind == DamageKind.Poison)
        {
            return Build(input.SkillBaseDamage, defender, input, critApplied);
        }

        var raw = ComputeRaw(attacker, input);
        var mitigated = MathF.Max(1f, raw - MitigationFloor(defender, input));

        if (input.Kind == DamageKind.PhysicalMelee && input.AttrPercent > 0 && input.Element != DamageElement.None)
        {
            var attrPct = input.AttrPercent / 100f;
            var attrLayer = (raw * attrPct) - (defender.Dp * _rates.Combat.PhysFactor * attrPct);
            attrLayer = MathF.Max(0f, attrLayer) * (1f - LookupResistance(defender, input.Element) / 100f);
            mitigated = (mitigated * (1f - LookupResistance(defender, DamageElement.None, fallbackPhysical: true) / 100f)) + attrLayer;
            return Build((int)MathF.Floor(mitigated), defender, input, critApplied, alreadyResisted: true);
        }

        return Build((int)MathF.Floor(mitigated), defender, input, critApplied);
    }

    private float ComputeRaw(AttackerStats attacker, DamageInput input)
    {
        var critMult = ResolveCritMultiplier(input);
        return input.Kind switch
        {
            DamageKind.PhysicalMelee => attacker.Ap * critMult,
            DamageKind.PhysicalSkill or DamageKind.PowerSkill => input.SkillBaseDamage * critMult,
            DamageKind.MagicSkill => input.SkillBaseDamage * critMult,
            DamageKind.Gun => MathF.Max(0f, 20f * (attacker.Ac - _rates.Combat.GunAccuracyOffset) + attacker.GunAp) * critMult,
            _ => 0f,
        };
    }

    private float ResolveCritMultiplier(DamageInput input)
    {
        if (!input.IsCrit) return 1f;
        if (input.IsMobAttack && !_rates.Combat.MobCanCrit) return 1f;
        return _rates.Combat.CritMultiplier;
    }

    private float MitigationFloor(DefenderStats defender, DamageInput input) => input.Kind switch
    {
        DamageKind.PhysicalMelee or DamageKind.PhysicalSkill or DamageKind.PowerSkill => _rates.Combat.PhysFactor * defender.Dp,
        DamageKind.MagicSkill => _rates.Combat.MagicFactor * defender.Md,
        DamageKind.Gun => _rates.Combat.GunFactor * defender.Hv,
        _ => 0f,
    };

    private DamageResult Build(int preResist, DefenderStats defender, DamageInput input, bool critApplied, bool alreadyResisted = false)
    {
        var resisted = alreadyResisted
            ? preResist
            : (int)MathF.Floor(preResist * (1f - LookupResistance(defender, input.Element, fallbackPhysical: input.Kind is DamageKind.PhysicalMelee or DamageKind.PhysicalSkill or DamageKind.PowerSkill, fallbackMagical: input.Kind == DamageKind.MagicSkill, fallbackGun: input.Kind == DamageKind.Gun) / 100f));

        var maxAllowed = (int)Math.Min(_rates.Caps.MaxDamagePerHit, ushort.MaxValue);
        var clamped = (ushort)Math.Clamp(resisted, 1, maxAllowed);
        var (flags, kind) = ResolveHit(input, critApplied);
        return new DamageResult(clamped, flags, kind);
    }

    private static float LookupResistance(
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
