using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public enum ChainTail
{
    TargetPointsThenSlots,
    SlotsOnly,
    VictimsWithSlots,
}

public sealed record ChainShape(ChainTail Tail, bool TargetInHeader, bool ScaledTm, bool Restarts);

public sealed record ChainVictim(ushort EntityId, IReadOnlyList<SkillHit> Hits)
{
    public long Sum => Hits.Where(Counts).Sum(hit => hit.Damage);

    public int Landed => Hits.Count(Counts);

    private static bool Counts(SkillHit hit) => hit.Damage > 0 && hit.Kind != SkillDamage.KindMiss;
}

public static class ChainHits
{
    public const int QuadPunch = 1195;
    public const int TetraPunch = 1327;
    public const int TwoHitCombo = 1258;
    public const int FistFullOfGalders = 1329;
    public const int BlazingStrike = 1218;

    public const int PunchHits = 4;
    public const int GalderHitsCap = 4;
    public const int GaldersPerHit = 100;
    public const int ComboFirstSkill = 4001;
    public const int ComboSecondSkill = 4101;
    public const int BlazingHitsCap = 10;
    public const int BlazingVictimsCap = 5;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string CountColumn = "Count";

    private const double GalderPowerScale = 100.0;
    private const float ComboLevelStep = 0.2f;
    private const int ComboLevelOffset = 20;
    private const double ComboLevelFloor = 3.0;
    private const int AvoidWeight = 16;
    private const double KickShare = 0.75;
    private const int BlazingDexterityBase = 10;
    private const int BlazingDexterityStep = 6;

    private static readonly Dictionary<int, ChainShape> Shapes = new()
    {
        [QuadPunch] = new(ChainTail.TargetPointsThenSlots, TargetInHeader: false, ScaledTm: true, Restarts: true),
        [TetraPunch] = new(ChainTail.TargetPointsThenSlots, TargetInHeader: false, ScaledTm: true, Restarts: true),
        [TwoHitCombo] = new(ChainTail.SlotsOnly, TargetInHeader: true, ScaledTm: true, Restarts: true),
        [FistFullOfGalders] = new(ChainTail.TargetPointsThenSlots, TargetInHeader: false, ScaledTm: true, Restarts: false),
        [BlazingStrike] = new(ChainTail.VictimsWithSlots, TargetInHeader: false, ScaledTm: false, Restarts: false),
    };

    public static readonly AreaShape BlazingArea = new(AreaCentre.Target, BlazingVictimsCap, AreaTail.None, AreaTm.Flat)
    {
        LimitColumn = SkillAreaShapes.KillNumColumn, Target = AreaTarget.ReplacesFirstWhenMissing,
    };

    public static ChainShape? Find(int actionId) => Shapes.TryGetValue(actionId, out var shape) ? shape : null;

    public static bool IsChain(int actionId) => Shapes.ContainsKey(actionId);

    public static int GalderHits(SkillTableRow action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Math.Clamp(action.Amount(CountColumn) ?? 0, 0, GalderHitsCap);
    }

    public static int GalderCost(int hits) => hits * GaldersPerHit;

    public static int BlazingHits(long dexterity) =>
        (int)Math.Clamp((BlazingDexterityBase - dexterity) / BlazingDexterityStep + 1, 0L, BlazingHitsCap);

    public static ushort? Refusal(int actionId, SkillTableRow? action, long money, IReadOnlyDictionary<int, int> learned)
    {
        ArgumentNullException.ThrowIfNull(learned);
        return actionId switch
        {
            TwoHitCombo when learned.GetValueOrDefault(ComboFirstSkill) <= 0 || learned.GetValueOrDefault(ComboSecondSkill) <= 0 => SkillFailure.General,
            FistFullOfGalders when (long)(action?.Amount(CountColumn) ?? 0) * GaldersPerHit > money => SkillFailure.General,
            _ => null,
        };
    }

    public static long AttackPower(SkillTableRow action, SkillAttacker attacker)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Truncate(attacker.Ap * (double)(action.Value(RatioColumn) ?? 0f));
    }

    public static (long First, long Second) ComboPowers(SkillTableRow action, SkillAttacker attacker, int firstLevel, int secondLevel)
    {
        ArgumentNullException.ThrowIfNull(action);
        var ratio = (double)(action.Value(RatioColumn) ?? 0f);
        var first = ((firstLevel + ComboLevelOffset) * (double)ComboLevelStep - ComboLevelFloor) * (attacker.Ap * ratio);
        var second = ((secondLevel + ComboLevelOffset) * (double)ComboLevelStep - ComboLevelFloor)
            * (attacker.Ap + AvoidWeight * attacker.Hv) * ratio * KickShare;
        return (Truncate(first), Truncate(second));
    }

    public static long GalderPower(SkillTableRow action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Truncate((action.Value(RatioColumn) ?? 0f) * GalderPowerScale);
    }

    public static IReadOnlyList<SkillHit> Resolve(
        int actionId,
        SkillTableRow action,
        SkillAttacker attacker,
        SkillTarget target,
        IReadOnlyDictionary<int, int> learned,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(learned);
        ArgumentNullException.ThrowIfNull(rng);
        var coefficient = action.Value(ApplyRatioColumn) ?? 0f;
        var hits = new List<SkillHit>();
        switch (actionId)
        {
            case QuadPunch:
            case TetraPunch:
            {
                var power = AttackPower(action, attacker);
                var previous = 0L;
                for (var hitIndex = 0; hitIndex < PunchHits; hitIndex++)
                {
                    var hit = SkillDamage.Physical(attacker, power, target, coefficient, SkillActions.NoElement, rng);
                    if (actionId == TetraPunch)
                    {
                        if (hit.Damage > 0 && previous != 0) hit = hit with { Damage = previous << 1 };
                        previous = hit.Damage;
                    }
                    hits.Add(hit);
                }
                break;
            }
            case TwoHitCombo:
            {
                var (first, second) = ComboPowers(
                    action, attacker, learned.GetValueOrDefault(ComboFirstSkill), learned.GetValueOrDefault(ComboSecondSkill));
                hits.Add(SkillDamage.Physical(attacker, first, target, coefficient, SkillActions.NoElement, rng));
                hits.Add(SkillDamage.Physical(attacker, second, target, coefficient, SkillActions.NoElement, rng));
                break;
            }
            case FistFullOfGalders:
            {
                var power = GalderPower(action);
                var undefended = target with { Dp = 0 };
                for (var hitIndex = 0; hitIndex < GalderHits(action); hitIndex++)
                {
                    hits.Add(SkillDamage.Physical(attacker, power, undefended, coefficient, SkillActions.NoElement, rng));
                }
                break;
            }
            case BlazingStrike:
            {
                var power = AttackPower(action, attacker);
                for (var hitIndex = 0; hitIndex < BlazingHits(attacker.Dx); hitIndex++)
                {
                    hits.Add(SkillDamage.Physical(attacker, power, target, coefficient, SkillActions.NoElement, rng));
                }
                break;
            }
        }
        return hits;
    }

    private static long Truncate(double value) => WireLimits.Saturate(Math.Truncate(value));
}
