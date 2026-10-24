using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct GrowthAid(float ApplyRatio, float MinAbility, float MaxAbility);

public static class GrowthRules
{
    public const int FeeFactor = 5;
    public const int ChanceRoll = 100;
    public const int NoSlot = -1;
    public const int BrokenUsableOption = 0x40000;

    public static long Fee(ItemParamEntry target, float discount)
    {
        ArgumentNullException.ThrowIfNull(target);
        long fee = (long)target.Value * FeeFactor;
        return fee - (long)Math.Truncate(fee * (double)discount);
    }

    public static int FreeSlot(IReadOnlyList<ItemProperty> properties, int slotCount)
    {
        ArgumentNullException.ThrowIfNull(properties);
        if (properties.Count == 0) return NoSlot;
        var used = properties.Where(p => p.Type is >= 0 and <= ItemPropertyTypes.LastMixSlot).Select(p => p.Type).ToHashSet();
        if (used.Count >= slotCount) return NoSlot;
        for (var slot = 0; slot <= ItemPropertyTypes.LastMixSlot; slot++)
        {
            if (!used.Contains(slot)) return slot;
        }
        return NoSlot;
    }

    public static bool AidsClash(GrowthAid first, GrowthAid second) =>
        (first.ApplyRatio != 0 && second.ApplyRatio != 0)
        || (first.MinAbility != 0 && second.MinAbility != 0)
        || (first.MaxAbility != 0 && second.MaxAbility != 0);

    public static int Chance(float applyRatio, IReadOnlyList<GrowthAid> aids, int luck, int targetMinLevel, float compoundMasterRatio)
    {
        ArgumentNullException.ThrowIfNull(aids);
        var level = luck / (targetMinLevel + luck + 10.0) + luck / 100.0;
        if (compoundMasterRatio > 0) level *= compoundMasterRatio;
        return (int)Math.Truncate((applyRatio + aids.Sum(aid => aid.ApplyRatio)) * level * 1.0 * 100.0);
    }

    public static (int Min, int Max) Deltas(MaturingCompoundRow row, IReadOnlyList<GrowthAid> aids)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(aids);
        var sumMin = aids.Sum(aid => aid.MinAbility);
        var sumMax = aids.Sum(aid => aid.MaxAbility);
        return (Delta(row.MinAbility, sumMin), Delta(row.MaxAbility, sumMax));
    }

    public static short Ability(MaturingCompoundRow row, (int Min, int Max) deltas, int curveBin)
    {
        ArgumentNullException.ThrowIfNull(row);
        var low = row.MinAbility + deltas.Min;
        var high = row.MaxAbility + deltas.Max;
        return (short)(low + (int)Math.Truncate((high - low) / 100.0 * curveBin));
    }

    public static short FailedAbility(MaturingCompoundRow row, (int Min, int Max) deltas)
    {
        ArgumentNullException.ThrowIfNull(row);
        return unchecked((short)(ushort)(row.MinAbility + deltas.Min));
    }

    private static int Delta(int ability, float sum)
    {
        var delta = (int)Math.Truncate(ability * (double)sum);
        if (sum == 0) return delta;
        if (ability > 0) return Math.Max(delta, 1);
        if (ability < 0) return Math.Min(delta, -1);
        return delta;
    }
}
