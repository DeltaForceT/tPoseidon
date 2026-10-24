namespace tPoseidon.Core.Game.Skills.Effects;

public enum SkillEffectRemoval
{
    Replaced,
    Evicted,
}

public sealed record SkillEffectRegistration(IReadOnlyList<(SkillEffect Effect, SkillEffectRemoval Reason)> Removed);

public readonly record struct SkillEffectCaster(int Ap, int Hv, int Ma = 0, int Da = 0, int Ac = 0, int Lk = 0);

public sealed class SkillEffectBook
{
    public static readonly IReadOnlyDictionary<int, int> PlayerCapacities = new Dictionary<int, int>
    {
        [0] = 10, [1] = 5, [2] = 5, [3] = 5, [4] = 5, [7] = 5,
    };

    public static readonly IReadOnlyDictionary<int, int> MonsterCapacities = new Dictionary<int, int>
    {
        [0] = 10, [1] = 5, [2] = 5, [3] = 0, [4] = 1, [7] = 0,
    };

    private const int SixteenthDivisor = 16;
    private const double AcDownFloor = 49.0;
    private const double MarksmanshipDivisor = 20.0;
    private const double PercentDivisor = 100.0;
    private const double LowLifeWeight = 2.5;
    private const int MagicFloor = 0x31;
    private const double NonPlayerDivisor = 2.5;

    private readonly IReadOnlyDictionary<int, int> _capacities;
    private readonly List<SkillEffect> _effects = new();
    private readonly object _gate = new();
    private long _sequence;

    public SkillEffectBook(IReadOnlyDictionary<int, int> capacities)
    {
        _capacities = capacities ?? throw new ArgumentNullException(nameof(capacities));
    }

    public IReadOnlyList<SkillEffect> Snapshot()
    {
        lock (_gate) return _effects.ToArray();
    }

    public SkillEffectRegistration? Register(SkillEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        lock (_gate)
        {
            var removed = new List<(SkillEffect, SkillEffectRemoval)>();
            if (effect.EsClass < 0)
            {
                effect.Sequence = ++_sequence;
                _effects.Add(effect);
                return new SkillEffectRegistration(removed);
            }
            var group = GroupOf(effect.EsClass);
            var sameAction = _effects.FirstOrDefault(held => GroupOf(held.EsClass) == group && held.Kind.ActionId == effect.Kind.ActionId);
            if (sameAction is not null)
            {
                _effects.Remove(sameAction);
                removed.Add((sameAction, SkillEffectRemoval.Replaced));
            }
            else if (_effects.Count(held => GroupOf(held.EsClass) == group) >= CapacityOf(group))
            {
                var oldest = _effects.Where(held => GroupOf(held.EsClass) == group).MinBy(held => held.Sequence);
                if (oldest is null) return null;
                _effects.Remove(oldest);
                removed.Add((oldest, SkillEffectRemoval.Evicted));
            }
            effect.Sequence = ++_sequence;
            _effects.Add(effect);
            return new SkillEffectRegistration(removed);
        }
    }

    public bool Remove(SkillEffect effect)
    {
        lock (_gate) return _effects.Remove(effect);
    }

    public IReadOnlyList<SkillEffect> Clear()
    {
        lock (_gate)
        {
            var all = _effects.ToArray();
            _effects.Clear();
            return all;
        }
    }

    public int[] Deltas(IReadOnlyList<int> bases, Func<ushort, SkillEffectCaster?> casterOf, bool ownerIsPlayer = true)
    {
        ArgumentNullException.ThrowIfNull(bases);
        ArgumentNullException.ThrowIfNull(casterOf);
        var accumulated = new float[SkillEffectKinds.StatCount];
        foreach (var effect in Snapshot())
        {
            if (!effect.IsApplied) continue;
            foreach (var (morphStat, morphDelta) in effect.FixedDeltas) accumulated[morphStat] += morphDelta;
            if (!effect.Kind.ChangesStat) continue;
            var stat = effect.Kind.Stat;
            accumulated[stat] = Apply(effect, (float)bases[stat], accumulated[stat], casterOf, ownerIsPlayer, null, stat);
            if (effect.Kind.SecondStat is var second && second != SkillEffectKinds.NoStat)
            {
                accumulated[second] = Apply(effect, (float)bases[second], accumulated[second], casterOf, ownerIsPlayer, effect.SecondAmount, second);
            }
            if (effect.Kind.ThirdStat is var third && third != SkillEffectKinds.NoStat)
            {
                accumulated[third] = Apply(effect, (float)bases[third], accumulated[third], casterOf, ownerIsPlayer, effect.ThirdAmount, third);
            }
        }
        return accumulated.Select(value => (int)Math.Truncate(value)).ToArray();
    }

    public static float Apply(
        SkillEffect effect, float statBase, float accumulated, Func<ushort, SkillEffectCaster?> casterOf, bool ownerIsPlayer = true,
        float? ratioOverride = null, int stat = SkillEffectKinds.NoStat)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(casterOf);
        double ratio = ratioOverride ?? (effect.Extra == 0f ? effect.Amount : effect.Amount + effect.Extra);
        switch (effect.Kind.Rule)
        {
            case SkillEffectRule.NoChange:
                return accumulated;
            case SkillEffectRule.RatioOfBase:
                return (float)(statBase * ratio + accumulated) + effect.Flat;
            case SkillEffectRule.Flat:
                return accumulated + (int)effect.Amount;
            case SkillEffectRule.BestPartyDefence:
                return accumulated + effect.Amount;
            case SkillEffectRule.DoubleBase:
                return (float)Math.Floor(statBase) + statBase;
            case SkillEffectRule.FixedAtNegative:
                return -effect.Amount - statBase;
            case SkillEffectRule.AttackRatioMinusOne:
                return statBase > 0f ? (float)(statBase * ratio - statBase + accumulated) : accumulated;
            case SkillEffectRule.LowLifeAttack:
                return effect.Active ? (float)(statBase * (ratio - LowLifeWeight * effect.LifeRatio)) + accumulated : accumulated;
            case SkillEffectRule.FlatTwentieth:
                return (float)((int)effect.Amount / MarksmanshipDivisor + accumulated);
            case SkillEffectRule.OwnerDefence:
            {
                var reduction = (float)(statBase * ratio);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
        }
        if (casterOf(effect.CasterEntityId) is not { } caster) return accumulated;
        switch (effect.Kind.Rule)
        {
            case SkillEffectRule.CasterAttack:
                return (float)(accumulated - caster.Ap * ratio);
            case SkillEffectRule.CasterAttackSixteenthPlusFortyNine:
                return (float)(accumulated - (caster.Ap * ratio / SixteenthDivisor + AcDownFloor));
            case SkillEffectRule.CasterAttackSixteenth:
            {
                var reduction = (float)(caster.Ap * ratio / SixteenthDivisor);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
            case SkillEffectRule.CasterAvoidSixteenfold:
            {
                var reduction = (float)(caster.Hv * ratio * SixteenthDivisor);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
            case SkillEffectRule.CasterAvoid:
            {
                var reduction = (float)(caster.Hv * ratio);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
            case SkillEffectRule.CasterMagicHundredthAdded:
            {
                var increase = (float)(caster.Ma * ratio / PercentDivisor);
                return increase > 0f ? accumulated + increase : accumulated;
            }
            case SkillEffectRule.CasterMagicOverFortyNineSixteenfold:
            {
                var reduction = (float)((float)((caster.Ma - MagicFloor) << 4) * ratio);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
            case SkillEffectRule.CasterDig:
            {
                var reduction = ownerIsPlayer ? (float)(caster.Da * ratio) : (float)(caster.Da * ratio / NonPlayerDivisor);
                return reduction > 0f ? accumulated - reduction : accumulated;
            }
            case SkillEffectRule.CasterLuckDown:
            {
                var loss = (double)(float)(caster.Lk * ratio);
                if (stat == SkillEffectKinds.StatHv && !ownerIsPlayer) loss *= SkillEffectKinds.MonsterAvoidShare;
                return loss > 0.0 ? (float)(accumulated - loss) : accumulated;
            }
            case SkillEffectRule.OwnerMagicSixteenfold:
            {
                var gain = (float)((float)((caster.Ma - MagicFloor) << 4) * ratio);
                return gain > 0f ? accumulated + gain : accumulated;
            }
            case SkillEffectRule.CasterAccuracy:
                return ownerIsPlayer
                    ? (float)(accumulated - caster.Ac * ratio)
                    : (float)(accumulated - caster.Ac * ratio / NonPlayerDivisor);
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Kind.Rule, "Unknown skill effect rule.");
        }
    }

    public static float ProjectedDefence(int bestDefence, float percent) => (float)(bestDefence * percent / PercentDivisor);

    public static SkillEffectMode RollMode(float applyRatio, int resistPercent, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        if (applyRatio == 0f) return SkillEffectMode.Applied;
        var chance = Math.Clamp((int)Math.Truncate(applyRatio * 100.0), 0, 100);
        if (rng.Next(100) >= chance) return SkillEffectMode.Failed;
        return rng.Next(100) < Math.Clamp(100 - resistPercent, 0, 100) ? SkillEffectMode.Applied : SkillEffectMode.Resisted;
    }

    public static bool AllowsCast(SkillEffectKind kind, float hpLimit, long hp, long maxHp)
    {
        ArgumentNullException.ThrowIfNull(kind);
        if (kind.HpLimitColumn is null) return true;
        return maxHp > 0 && hp <= maxHp * (double)hpLimit;
    }

    private int CapacityOf(int group) => _capacities.TryGetValue(group, out var capacity) ? capacity : 0;

    private static int GroupOf(sbyte esClass) => esClass is 5 or 6 ? 4 : esClass;
}
