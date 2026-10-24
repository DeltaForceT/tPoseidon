using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct EquipMergeRequest(ulong MainWireUid, ulong SubWireUid, ulong SupportWireUid);

public enum EquipMergeMode
{
    Random,
    Better,
}

public static class EquipMergeRules
{
    public const string SupportTable = "EquipFusionSupportItemTable";
    public const int BaseSupportRow = 0;
    public const int MaxRatio = 100;
    public const int RollRange = 10000;
    public const int CostFactor = 2;
    public const int FirstStatId = 10000;
    public const int LastStatId = 10012;
    public const int LowerIsBetterStatId = 10002;

    public const byte Ok = 0;
    public const byte NotHeld = 1;
    public const byte Equipped = 2;
    public const byte NotEquipment = 3;
    public const byte NotSame = 4;
    public const byte NotMergeable = 5;
    public const byte Failed = 7;
    public const byte BadSupport = 10;

    public static readonly int[] MergeableTypes = [2, 3, 4, 7, 8, 0x18, 0x19, 0x22];

    public static EquipMergeMode? ModeOf(int refineIndex) => refineIndex switch
    {
        35 or 54 => EquipMergeMode.Random,
        64 => EquipMergeMode.Better,
        _ => null,
    };

    public static byte Check(ItemParamEntry main, ItemParamEntry sub)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(sub);
        if (!MergeableTypes.Contains(main.Type) || !MergeableTypes.Contains(sub.Type)) return NotEquipment;
        if (main.Id != sub.Id) return NotSame;
        return ModeOf(main.RefineIndex) is null ? NotMergeable : Ok;
    }

    public static int Chance(IReadOnlyDictionary<int, int> supports, int? supportItemId)
    {
        ArgumentNullException.ThrowIfNull(supports);
        var chance = Math.Min(MaxRatio, supports.TryGetValue(BaseSupportRow, out var baseRatio) ? baseRatio : 0);
        if (supportItemId is int id && supports.TryGetValue(id, out var supportRatio)) chance += Math.Min(MaxRatio, supportRatio);
        return chance;
    }

    public static bool Succeeds(int chance, int roll) => chance * (RollRange / MaxRatio) >= roll;

    public static long Cost(ItemParamEntry main)
    {
        ArgumentNullException.ThrowIfNull(main);
        return (long)main.Value * CostFactor;
    }

    public static bool IsMergedStat(ItemProperty property) =>
        property.Type == ItemPropertyTypes.Stat && property.Id is >= FirstStatId and <= LastStatId;

    public static IReadOnlyList<ItemProperty> MergeStats(IReadOnlyList<ItemProperty> main, IReadOnlyList<ItemProperty> sub, EquipMergeMode mode, Func<int, int, int> between)
    {
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(sub);
        ArgumentNullException.ThrowIfNull(between);
        var result = main.Where(property => !IsMergedStat(property)).ToList();
        var ids = main.Concat(sub).Where(IsMergedStat).Select(property => property.Id).Distinct().OrderBy(id => id);
        foreach (var id in ids)
        {
            var mine = main.FirstOrDefault(property => IsMergedStat(property) && property.Id == id);
            var theirs = sub.FirstOrDefault(property => IsMergedStat(property) && property.Id == id);
            var hasMine = IsMergedStat(mine);
            var hasTheirs = IsMergedStat(theirs);
            short value;
            if (hasMine && hasTheirs) value = Merge(id, mine.Value, theirs.Value, mode, between);
            else if (hasMine) value = mine.Value;
            else value = mode == EquipMergeMode.Better ? theirs.Value : Merge(id, 0, theirs.Value, EquipMergeMode.Random, between);
            result.Add(new ItemProperty(ItemPropertyTypes.Stat, id, value, hasMine ? mine.Extra : theirs.Extra));
        }
        return result;
    }

    private static short Merge(int id, short a, short b, EquipMergeMode mode, Func<int, int, int> between)
    {
        if (mode == EquipMergeMode.Better) return id == LowerIsBetterStatId ? Math.Min(a, b) : Math.Max(a, b);
        var low = Math.Min(a, b);
        var high = Math.Max(a, b);
        return low == high ? low : (short)between(low, high);
    }

    public static byte MergeMixSlots(byte main, byte sub, EquipMergeMode mode, Func<int, int, int> between)
    {
        if (mode == EquipMergeMode.Better) return Math.Max(main, sub);
        var low = Math.Min(main, sub);
        var high = Math.Max(main, sub);
        return low == high ? low : (byte)between(low, high);
    }
}
