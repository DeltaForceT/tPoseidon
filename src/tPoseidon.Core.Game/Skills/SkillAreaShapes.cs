using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public enum AreaCentre
{
    Caster,
    Target,
}

public enum AreaTarget
{
    Ignored,
    AlwaysFirst,
    ReplacesFirstWhenMissing,
    FirstWhenMissing,
    ExtraNearTarget,
}

public enum AreaTail
{
    CountThenIds,
    TargetCountThenIds,
    TargetOnly,
    ByteCountThenIds,
    Radius,
    ExtraFlag,
    None,
}

public enum AreaTm
{
    None,
    Flat,
    Scaled,
}

public sealed record AreaShape(AreaCentre Centre, int Limit, AreaTail Tail, AreaTm Tm)
{
    public string? LimitColumn { get; init; }
    public AreaTarget Target { get; init; } = AreaTarget.Ignored;
    public bool Cone { get; init; }
    public bool ConeFromCaster { get; init; }
    public bool Falloff { get; init; }
    public bool SkipsFlyers { get; init; }
    public bool RestartsOnFirst { get; init; }
    public SkillHitKind Kind { get; init; } = SkillHitKind.Physical;
    public SkillAreaPower Power { get; init; } = SkillAreaPower.AttackTimesRatio;
    public int Element { get; init; }
    public int MagicLoss { get; init; }
    public bool MagicLossLessAccuracy { get; init; }
    public bool CoefficientIsRatio { get; init; }
    public int ElementSlot { get; init; } = ElementSlots.None;
}

public enum SkillAreaPower
{
    AttackTimesRatio,
    MagicPower,
    CasterLifeAndDefence,
    AttackAndAvoidThreeQuarters,
    AttackAndDigRatio,
    CarriedWeight,
}

public readonly record struct AreaCandidate(ushort EntityId, int X, int Y, bool IsDead, int MonsterType, ushort OwnerEntityId);

public static class SkillAreaShapes
{
    public const string RadiusColumn = "Radius";
    public const string KillNumColumn = "KillNum";
    public const string MonNumColumn = "MonNum";
    public const string CountColumn = "Count";
    public const string AngleColumn = "Angle";
    public const string DigRatioColumn = "DaRatio";

    public const int PouchOfPain = 1214;
    public const int QuakeHammer = 1530;
    public const int HighSpeedDrop = 1546;
    public const int PolarBearAreaLimit = 6;
    public const int HammerWheel = 1547;
    public const int RampageWheelSkill = 4909;

    public static readonly AreaShape HammerWheelTick = new(AreaCentre.Caster, PolarBearAreaLimit, AreaTail.None, AreaTm.None)
    {
        LimitColumn = KillNumColumn, Power = SkillAreaPower.AttackAndDigRatio,
    };

    public const int FlyerMonsterType = 8;
    public const int UntouchableMonsterType = 12;

    private const double DegreesPerRadian = 57.29578f;
    private const double HalfTurn = 180.0;
    private const float LifeShare = 0.1f;
    private const double DefenceShare = 0.5;
    private const double ThreeQuarters = 0.75;

    private static readonly Dictionary<int, AreaShape> Shapes = new()
    {
        [1310] = new(AreaCentre.Caster, 5, AreaTail.CountThenIds, AreaTm.Flat),
        [PouchOfPain] = new(AreaCentre.Target, 5, AreaTail.TargetCountThenIds, AreaTm.Flat)
        {
            Power = SkillAreaPower.CarriedWeight, SkipsFlyers = true,
        },
        [1325] = new(AreaCentre.Caster, 5, AreaTail.CountThenIds, AreaTm.Flat)
        {
            LimitColumn = KillNumColumn, Target = AreaTarget.ReplacesFirstWhenMissing, RestartsOnFirst = true,
        },
        [1336] = new(AreaCentre.Caster, 7, AreaTail.CountThenIds, AreaTm.Flat)
        {
            LimitColumn = KillNumColumn, Target = AreaTarget.ReplacesFirstWhenMissing, RestartsOnFirst = true,
        },
        [1352] = new(AreaCentre.Caster, 7, AreaTail.CountThenIds, AreaTm.Flat) { LimitColumn = KillNumColumn },
        [1470] = new(AreaCentre.Caster, 6, AreaTail.ByteCountThenIds, AreaTm.Flat) { LimitColumn = KillNumColumn },
        [1481] = new(AreaCentre.Caster, 6, AreaTail.CountThenIds, AreaTm.Flat) { LimitColumn = KillNumColumn },
        [1263] = new(AreaCentre.Target, 5, AreaTail.CountThenIds, AreaTm.Flat)
        {
            Target = AreaTarget.FirstWhenMissing, Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower,
            Element = SkillHitShapes.ElectricElement, MagicLoss = SkillHitShapes.ThunderMagicLoss, ElementSlot = ElementSlots.Electric,
        },
        [1284] = new(AreaCentre.Caster, 3, AreaTail.CountThenIds, AreaTm.Flat)
        {
            Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower, Element = SkillHitShapes.WindElement,
            MagicLoss = SkillHitShapes.MagicLoss, MagicLossLessAccuracy = true, ElementSlot = ElementSlots.Wind,
        },
        [1381] = new(AreaCentre.Caster, 4, AreaTail.Radius, AreaTm.Flat)
        {
            LimitColumn = CountColumn, Power = SkillAreaPower.AttackAndAvoidThreeQuarters,
        },
        [1184] = new(AreaCentre.Target, 7, AreaTail.TargetOnly, AreaTm.None)
        {
            Falloff = true, SkipsFlyers = true, Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower,
            Element = 8, MagicLoss = SkillHitShapes.ThunderMagicLoss, ElementSlot = ElementSlots.Earth,
        },
        [1190] = new(AreaCentre.Target, 7, AreaTail.TargetCountThenIds, AreaTm.None)
        {
            Target = AreaTarget.AlwaysFirst, Cone = true, SkipsFlyers = true, Kind = SkillHitKind.Magic,
            Power = SkillAreaPower.MagicPower, Element = SkillHitShapes.LightElement, MagicLoss = SkillHitShapes.MagicLoss,
        },
        [1199] = new(AreaCentre.Target, int.MaxValue, AreaTail.TargetCountThenIds, AreaTm.None)
        {
            LimitColumn = MonNumColumn, Target = AreaTarget.AlwaysFirst, Cone = true, SkipsFlyers = true,
            Power = SkillAreaPower.CasterLifeAndDefence,
        },
        [1215] = new(AreaCentre.Caster, 7, AreaTail.CountThenIds, AreaTm.Flat)
        {
            LimitColumn = KillNumColumn, SkipsFlyers = true, Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower,
            Element = SkillHitShapes.DarkElement, MagicLoss = SkillHitShapes.MagicLoss,
        },
        [1256] = new(AreaCentre.Caster, 3, AreaTail.CountThenIds, AreaTm.Flat)
        {
            SkipsFlyers = true, Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower,
            Element = SkillHitShapes.LightElement, MagicLoss = SkillHitShapes.MagicLoss,
        },
        [1244] = new(AreaCentre.Target, 5, AreaTail.TargetCountThenIds, AreaTm.Flat)
        {
            Target = AreaTarget.AlwaysFirst, Cone = true, SkipsFlyers = true,
        },
        [1185] = new(AreaCentre.Target, 1, AreaTail.ExtraFlag, AreaTm.None)
        {
            Target = AreaTarget.ExtraNearTarget, SkipsFlyers = true, Kind = SkillHitKind.Magic, Power = SkillAreaPower.MagicPower,
            Element = SkillHitShapes.FireElement, MagicLoss = SkillHitShapes.MagicLoss, ElementSlot = ElementSlots.Fire,
        },
        [QuakeHammer] = new(AreaCentre.Caster, PolarBearAreaLimit, AreaTail.None, AreaTm.Flat)
        {
            LimitColumn = KillNumColumn, Power = SkillAreaPower.AttackAndDigRatio,
        },
        [HighSpeedDrop] = new(AreaCentre.Caster, PolarBearAreaLimit, AreaTail.None, AreaTm.Flat) { LimitColumn = KillNumColumn },
        [1261] = new(AreaCentre.Caster, 4, AreaTail.CountThenIds, AreaTm.Flat)
        {
            Target = AreaTarget.AlwaysFirst, Cone = true, ConeFromCaster = true, CoefficientIsRatio = true,
        },
    };

    public static AreaShape? Find(int actionId) => Shapes.TryGetValue(actionId, out var shape) ? shape : null;

    public static int LimitOf(AreaShape shape, SkillTableRow action)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(action);
        if (shape.LimitColumn is null) return shape.Limit;
        var fromRow = action.Amount(shape.LimitColumn) ?? 0;
        return Math.Max(0, Math.Min(shape.Limit, fromRow));
    }

    public static float Bearing(int fromX, int fromY, int toX, int toY)
    {
        float vx = toX - fromX;
        float vy = toY - fromY;
        var length = (float)Math.Sqrt(vx * vx + vy * vy);
        vx /= length;
        vy /= length;
        var dot = vy * vy;
        var angle = (float)(Math.Acos(Math.Cos(dot)) * DegreesPerRadian);
        return vx > 0f ? angle : (float)(angle + HalfTurn);
    }

    public static bool InCone(int apexFromX, int apexFromY, int apexX, int apexY, int candidateX, int candidateY, float half)
    {
        var heading = Bearing(apexFromX, apexFromY, apexX, apexY);
        var toCandidate = Bearing(apexX, apexY, candidateX, candidateY);
        return heading - half <= toCandidate && toCandidate <= heading + half;
    }

    public static IReadOnlyList<ushort> Collect(
        AreaShape shape,
        SkillTableRow action,
        ushort casterEntityId,
        (int X, int Y) casterAt,
        AreaCandidate? selected,
        IEnumerable<AreaCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(candidates);
        var radius = (float)(action.Amount(RadiusColumn) ?? 0);
        var limit = LimitOf(shape, action);
        var angle = action.Value(AngleColumn) ?? 0f;
        var centre = shape.Centre == AreaCentre.Target && selected is { } selectedCentre ? (selectedCentre.X, selectedCentre.Y) : casterAt;
        var victims = new List<ushort>();
        if (shape.Target == AreaTarget.AlwaysFirst && selected is { } first) victims.Add(first.EntityId);

        foreach (var candidate in candidates)
        {
            if (shape.Target == AreaTarget.AlwaysFirst && candidate.EntityId == selected?.EntityId) continue;
            if (shape.Target == AreaTarget.ExtraNearTarget && candidate.EntityId == selected?.EntityId) continue;
            if (!Qualifies(shape, candidate, casterEntityId)) continue;
            var distanceFrom = shape.ConeFromCaster ? casterAt : centre;
            if (radius < SkillAreaRules.Distance(distanceFrom.X, distanceFrom.Y, candidate.X, candidate.Y)) continue;
            if (victims.Count >= limit) break;
            if (shape.Cone && selected is { } apex
                && !InCone(casterAt.X, casterAt.Y, apex.X, apex.Y, candidate.X, candidate.Y, angle))
            {
                continue;
            }
            victims.Add(candidate.EntityId);
        }

        if (selected is { } target)
        {
            switch (shape.Target)
            {
                case AreaTarget.ReplacesFirstWhenMissing when !victims.Contains(target.EntityId):
                    if (victims.Count == 0) victims.Add(target.EntityId);
                    else victims[0] = target.EntityId;
                    break;
                case AreaTarget.FirstWhenMissing when !victims.Contains(target.EntityId) && victims.Count > 0:
                    victims[0] = target.EntityId;
                    break;
                case AreaTarget.ExtraNearTarget:
                    victims.Insert(0, target.EntityId);
                    break;
            }
        }
        return victims;
    }

    public static int Power(AreaShape shape, SkillTableRow action, SkillAttacker attacker)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(action);
        var ratio = action.Value(SkillHitShapes.RatioColumn) ?? 0f;
        return shape.Power switch
        {
            SkillAreaPower.AttackTimesRatio => Truncate(attacker.Ap * (double)ratio),
            SkillAreaPower.MagicPower => (action.Amount(SkillHitShapes.PowerColumn) ?? 0) + attacker.ElementBonus,
            SkillAreaPower.CasterLifeAndDefence => Truncate((attacker.Hp * (double)LifeShare + attacker.Dp * DefenceShare) * ratio),
            SkillAreaPower.AttackAndAvoidThreeQuarters => Truncate((attacker.Ap + 16 * attacker.Hv) * (double)ratio * ThreeQuarters),
            SkillAreaPower.AttackAndDigRatio => Truncate(attacker.Ap * ((double)ratio + attacker.Da * (double)(action.Value(DigRatioColumn) ?? 0f))),
            SkillAreaPower.CarriedWeight => Truncate(attacker.Weight * (double)ratio),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.Power, "Unknown area power rule."),
        };
    }

    public static SkillHit Hit(AreaShape shape, SkillTableRow action, SkillAttacker attacker, SkillTarget victim, Random rng)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(rng);
        var coefficient = shape.CoefficientIsRatio
            ? action.Value(SkillHitShapes.RatioColumn) ?? 0f
            : action.Value(SkillHitShapes.ApplyRatioColumn) ?? 0f;
        var power = Power(shape, action, attacker);
        if (shape.Kind == SkillHitKind.Magic)
        {
            var loss = shape.MagicLossLessAccuracy ? shape.MagicLoss - attacker.Ac : shape.MagicLoss;
            return SkillDamage.Magic(attacker, power, victim, coefficient, loss, shape.Element, rng);
        }
        return SkillDamage.Physical(attacker, power, victim, coefficient, shape.Element, rng);
    }

    private static bool Qualifies(AreaShape shape, AreaCandidate candidate, ushort casterEntityId)
    {
        if (candidate.IsDead || candidate.MonsterType == UntouchableMonsterType) return false;
        if (shape.SkipsFlyers && candidate.MonsterType == FlyerMonsterType) return false;
        return candidate.OwnerEntityId == 0 || candidate.OwnerEntityId == casterEntityId;
    }

    private static int Truncate(double value) => (int)Math.Clamp(Math.Truncate(value), int.MinValue, int.MaxValue);
}
