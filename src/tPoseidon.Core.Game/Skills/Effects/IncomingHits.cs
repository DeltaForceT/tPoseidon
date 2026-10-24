namespace tPoseidon.Core.Game.Skills.Effects;

public sealed record IncomingHit(int Damage, int Type, int Kind, int Element, uint Flags, ushort Extra = 0);

public sealed record HitReflection(int Damage, int Type, int Kind, int Element, uint Flags);

public sealed record CounterStrike(SkillEffect Effect, int Power, float Coefficient, int MagicLoss, int Element);

public sealed record HitInterception(
    IncomingHit Hit,
    IReadOnlyList<SkillEffect> ShieldsTouched,
    IReadOnlyList<HitReflection> Reflections,
    IReadOnlyList<CounterStrike> Counters);

public readonly record struct HitDefender(ushort EntityId, int Ma, int Ac, int Lk, int Hv);

public static class IncomingHits
{
    public const int BasicAttackType = 4;
    public const int OtherType = 1;
    public const int PhysicalType = 2;
    public const int MagicType = 3;
    public const int ItemType = 5;

    public const int KindMiss = 1;
    public const int KindGuarded = 2;
    public const int KindBlockedByGuard = 6;
    public const int KindCounter = 7;
    public const int KindAbsorbed = 8;
    public const int KindUntouchable = 9;
    public const int KindUntouchableOther = 10;

    public const uint FlagReflected = 0x01;
    public const uint FlagReduced = 0x02;
    public const uint FlagBarrier = 0x04;
    public const uint FlagCounter = 0x08;
    public const uint FlagUnabsorbable = 0x1000;
    public const uint FlagUnabsorbableOther = 0x2000;
    public const uint FlagMultiHit = 0x8000;
    public const int BuffCancelFlag = 0x4000;

    private const double KnightShare = 0.5;
    private const double ReflectAvoidScale = 100.0;
    private const double ReflectFloor = 0.3;
    private const double ReflectCap = 0.8;
    private const int BarrierMagicFloor = 49;
    private const double BarrierPercent = 0.01;
    private const double BarrierCap = 0.5;
    private const double ReductionFloor = 20.0;
    private const double ReductionDivisor = 3.0;
    private const float ReductionPercent = 100.0f;
    private const float ReductionCap = 0.5f;
    private const double CounterShare = 0.5;

    public static HitInterception Intercept(
        IncomingHit hit, IEnumerable<SkillEffect> effects, HitDefender owner, Func<ushort, int> casterHv)
    {
        ArgumentNullException.ThrowIfNull(hit);
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(casterHv);
        var shields = new List<SkillEffect>();
        var reflections = new List<HitReflection>();
        var counters = new List<CounterStrike>();
        foreach (var effect in effects)
        {
            if (!effect.IsApplied) continue;
            var kind = effect.Kind;
            if (kind.EndsWhenOwnerHit)
            {
                effect.EndNow();
                continue;
            }
            if (kind.GuardsHits && effect.Charges > 0)
            {
                if ((hit.Flags & FlagMultiHit) != 0)
                {
                    effect.Charges = effect.Charges <= hit.Extra ? 0 : effect.Charges - hit.Extra;
                }
                else if (hit.Damage == 0 || hit.Kind is KindMiss or KindGuarded || hit.Type is MagicType or OtherType || Unabsorbable(hit))
                {
                    continue;
                }
                else
                {
                    effect.Charges--;
                }
                if (effect.Charges <= 0)
                {
                    effect.Charges = 0;
                    effect.EndNow();
                }
                hit = hit with { Damage = 0, Kind = KindBlockedByGuard, Extra = (ushort)effect.Charges };
                continue;
            }
            if (kind.AbsorbsHits && effect.Charges > 0)
            {
                if (hit.Damage == 0 || hit.Kind is KindMiss or KindGuarded or KindUntouchable || Unabsorbable(hit)) continue;
                var left = effect.Charges - hit.Damage;
                if (left < 1)
                {
                    hit = hit with { Damage = Math.Abs(left) };
                    effect.Charges = 0;
                    effect.EndNow();
                }
                else
                {
                    effect.Charges = left;
                    hit = hit with { Damage = 0, Kind = KindAbsorbed };
                }
                shields.Add(effect);
                continue;
            }
            if (kind.SharesHits && effect.CasterEntityId != owner.EntityId)
            {
                if (hit.Flags == 0 && hit.Damage > 0) hit = hit with { Damage = (int)(hit.Damage * KnightShare) };
                continue;
            }
            if (kind.ReducesSkillHits)
            {
                if (Unabsorbable(hit)) continue;
                if ((hit.Flags & FlagReduced) == 0)
                {
                    if (hit.Type is not (BasicAttackType or ItemType))
                    {
                        var reduction = SkinReduction(hit.Damage, casterHv(effect.CasterEntityId), effect.Amount);
                        if (reduction > 0) hit = hit with { Damage = hit.Damage - reduction };
                    }
                    hit = hit with { Flags = hit.Flags | FlagReduced };
                }
                continue;
            }
            if (kind.ReflectsMagic)
            {
                if ((hit.Flags & FlagReflected) == 0 && hit.Type is OtherType or MagicType)
                {
                    var reflected = MirrorShare(hit.Damage, owner.Hv, effect.Amount);
                    hit = hit with { Damage = hit.Damage - reflected };
                    reflections.Add(new HitReflection(reflected, hit.Type, hit.Kind, hit.Element, hit.Flags | FlagReflected));
                }
                continue;
            }
            if (kind.ReflectColumn is { } reflectColumn)
            {
                if (hit.Kind is KindUntouchable or KindUntouchableOther) continue;
                if ((hit.Flags & FlagBarrier) == 0 && hit.Type is PhysicalType or 0)
                {
                    var reflected = BarrierShare(hit.Damage, owner.Ma, effect.Action?.Value(reflectColumn) ?? 0f);
                    if (!Unabsorbable(hit)) hit = hit with { Damage = hit.Damage - reflected };
                    if (reflected > 0) reflections.Add(new HitReflection(reflected, hit.Type, hit.Kind, hit.Element, hit.Flags | FlagBarrier));
                    hit = hit with { Flags = hit.Flags | FlagBarrier };
                }
                continue;
            }
            if (kind.CounterElement != 0 && (hit.Flags & FlagCounter) == 0)
            {
                counters.Add(new CounterStrike(
                    effect,
                    (int)(effect.Amount * CounterShare),
                    effect.Action?.Value(SkillEffectKinds.ApplyRatioColumn) ?? 0f,
                    owner.Ma - (owner.Ac + owner.Lk),
                    kind.CounterElement));
            }
        }
        return new HitInterception(hit, shields, reflections, counters);
    }

    public static int SkinReduction(int damage, int casterHv, float ratio)
    {
        var points = (int)((casterHv * (double)ratio + ReductionFloor) / ReductionDivisor);
        var share = Math.Min(ReductionCap, points / ReductionPercent);
        return (int)(damage * (double)share);
    }

    public static int MirrorShare(int damage, int ownerHv, float ratio)
    {
        var share = Math.Min(ReflectCap, (ownerHv / ReflectAvoidScale + ReflectFloor) * ratio);
        return (int)(damage * share);
    }

    public static int BarrierShare(int damage, int ownerMa, float reflexRatio) =>
        Math.Min((int)(damage * (double)(ownerMa - BarrierMagicFloor) * reflexRatio * BarrierPercent), (int)(damage * BarrierCap));

    private static bool Unabsorbable(IncomingHit hit) => (hit.Flags & (FlagUnabsorbable | FlagUnabsorbableOther)) != 0;
}
