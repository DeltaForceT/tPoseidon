using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public static class RefineRules
{
    public const int MaxLevel = 13;
    public const int CashItemOption = 0x40000;
    public const int CashFeeFactor = 100;
    public const byte Broken = 1;
    public const int RollRange = 10000;
    public const double MaxChance = 100.0;

    private static readonly int[] PropertyIds = { 0, 10000, 10010, 10004, 10005, 10012, 10007, 10011 };

    public static int? PropertyIdFor(int refineType) =>
        refineType is >= 1 and <= RefineTables.Types ? PropertyIds[refineType] : null;

    public static bool CanRefine(ItemParamEntry definition, Item item)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        return item.RefineLevel < MaxLevel
            && definition.RefineType is >= 1 and <= RefineTables.Types
            && item.RefineState != Broken;
    }

    public static long Fee(ItemParamEntry definition, int level, float discount)
    {
        ArgumentNullException.ThrowIfNull(definition);
        long fee = (definition.Options & CashItemOption) != 0
            ? (long)definition.MinLevel * (level + 1) * CashFeeFactor
            : (long)definition.Value * (level + 1);
        return fee - (long)Math.Truncate(fee * (double)discount);
    }

    public static RefineMaterialRow? MaterialRow(RefineTables tables, int refineType, int minLevel)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var index = refineType - 1;
        if (index < 0 || index >= tables.Materials.Count) return null;
        return tables.Materials[index].FirstOrDefault(row => row.MinLevel <= minLevel && minLevel <= row.MaxLevel);
    }

    public static double Chance(float ratio, int fortune, float refineSeed, float supportProb)
    {
        var luck = fortune <= 0 ? 0.0 : refineSeed / 100.0 * fortune;
        var chance = Math.Truncate((double)ratio) + ratio * (luck * 0.01) + supportProb;
        return Math.Min(chance, MaxChance) * 100.0;
    }

    public static short Bonus(short baseValue, int accumulation) => (short)(baseValue * accumulation / 100);
}

public static class RepairRules
{
    public const int MaterialSubType = 63;
    public const int PhantomPowderId = 64799;
    public const int NeoRefineIndex = 70;
    public const int PhantomMaxLevel = 185;
    public const int PhantomPowderCount = 1;
    public const byte Repaired = 0;

    public const byte NoError = 0;
    public const byte InvalidPlayerState = 1;
    public const byte ImpossibleItem = 2;
    public const byte EquippedItem = 3;
    public const byte NoMaterial = 4;
    public const byte UnknownError = 5;

    public static bool IsNeo(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.RefineIndex == NeoRefineIndex;
    }

    public static byte Check(ItemParamEntry definition, Item item, bool phantomWindow)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        if (definition.ObjectClass != ItemParamEntry.RareClass || item.RefineState != RefineRules.Broken) return ImpossibleItem;
        if (IsNeo(definition) && (!phantomWindow || definition.MinLevel > PhantomMaxLevel)) return ImpossibleItem;
        return NoError;
    }

    public static int MaterialCount(ItemParamEntry definition, Item item) =>
        IsNeo(definition) ? PhantomPowderCount : Math.Max(1, (int)item.RefineLevel);

    public static bool IsMaterialFor(ItemParamEntry target, ItemParamEntry material) =>
        material.SubType == MaterialSubType && (material.Id == PhantomPowderId) == IsNeo(target);
}
