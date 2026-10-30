using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct SpeedBonus(float Walk, float Run, float Chase)
{
    public static SpeedBonus None => default;

    public float Of(byte effectiveMoveType) => effectiveMoveType switch
    {
        MeleeApproach.ChaseMoveType => Chase,
        MeleeApproach.WalkMoveType => Walk,
        _ => Run,
    };

    public float SpeedOf(MoveSpeedParamEntry speeds, byte effectiveMoveType) =>
        effectiveMoveType switch
        {
            MeleeApproach.ChaseMoveType => speeds.ChaseSpeed,
            MeleeApproach.WalkMoveType => speeds.WalkSpeed,
            _ => speeds.RunSpeed,
        } + Of(effectiveMoveType);

    public const float MembershipRatio = 0.2f;

    public static SpeedBonus ForMembership(MoveSpeedParamEntry baseSpeeds)
    {
        ArgumentNullException.ThrowIfNull(baseSpeeds);
        return new SpeedBonus(baseSpeeds.WalkSpeed * MembershipRatio, baseSpeeds.RunSpeed * MembershipRatio, baseSpeeds.ChaseSpeed * MembershipRatio);
    }

    public static SpeedBonus operator +(SpeedBonus left, SpeedBonus right) =>
        new(left.Walk + right.Walk, left.Run + right.Run, left.Chase + right.Chase);
}

public sealed class SpeedItemBook
{
    private readonly Dictionary<int, float> _ratios;

    public SpeedItemBook(IEnumerable<SpeedItemParamEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _ratios = new Dictionary<int, float>();
        foreach (var row in rows)
        {
            _ratios.TryAdd(row.ItemId, row.AddSpeedRatio);
        }
    }

    public int Count => _ratios.Count;

    public float? RatioOf(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.SubType == EquipmentRules.SpeedAccessorySubType && _ratios.TryGetValue(definition.Id, out var ratio) ? ratio : null;
    }

    public SpeedBonus BonusOf(IEnumerable<ItemParamEntry> worn, MoveSpeedParamEntry baseSpeeds)
    {
        ArgumentNullException.ThrowIfNull(worn);
        var total = SpeedBonus.None;
        foreach (var definition in worn)
        {
            if (RatioOf(definition) is { } ratio)
            {
                total += new SpeedBonus(baseSpeeds.WalkSpeed * ratio, baseSpeeds.RunSpeed * ratio, baseSpeeds.ChaseSpeed * ratio);
            }
        }

        return total;
    }
}
