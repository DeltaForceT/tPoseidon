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
    public const int RandRange = 0x8000;
    public const float SeedStep = 0.01f;
    public const double MaxChance = 100.0;
    public const int PhantomFreeExtendedOption = 0x1000;

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

    public static bool IsPhantomFree(ItemParamEntry definition, bool phantomWindow)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return phantomWindow && (definition.ExtendedOptions & PhantomFreeExtendedOption) != 0;
    }

    public static long Fee(ItemParamEntry definition, int level, float discount, bool phantomWindow = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (IsPhantomFree(definition, phantomWindow)) return 0;
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

    public static float Chance(float ratio, int fortune, float refineSeed, float supportProb)
    {
        var luck = fortune > 0 ? (float)(refineSeed / 100.0 * fortune) : 0f;
        var scaled = (float)(luck * (double)SeedStep);
        var chance = (float)(Math.Truncate((double)ratio) + ratio * (double)scaled);
        chance = (float)(chance + (double)supportProb);
        if (chance >= MaxChance) chance = (float)MaxChance;
        return (float)(chance * 100.0);
    }

    public static int Roll(Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.Next(RandRange) % RollRange;
    }

    public static bool Succeeds(float threshold, int roll) => roll < threshold;

    public static short Bonus(short baseValue, int accumulation) => (short)(baseValue * accumulation / 100);
}

public static class RepairRules
{
    public const byte Repaired = 0;

    public const byte NoError = 0;
    public const byte InvalidPlayerState = 1;
    public const byte ImpossibleItem = 2;
    public const byte EquippedItem = 3;
    public const byte NoMaterial = 4;
    public const byte UnknownError = 5;

    public static bool UsesNeoRow(ItemParamEntry definition, bool phantomWindow) => RefineRules.IsPhantomFree(definition, phantomWindow);

    public static byte Check(ItemParamEntry definition, Item item)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        return definition.ObjectClass != ItemParamEntry.RareClass || item.RefineState != RefineRules.Broken ? ImpossibleItem : NoError;
    }

    public static RepairRow? Material(RefineTables tables, ItemParamEntry definition, Item item, bool phantomWindow)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        var neo = UsesNeoRow(definition, phantomWindow);
        return tables.Repairs.FirstOrDefault(row => row.RefineLevel == item.RefineLevel
            && row.Type == definition.Type
            && row.SubType == definition.SubType
            && row.NeoEquip == neo);
    }

    public static IReadOnlyList<(Item Stack, int Take)>? Gather(IEnumerable<Item> bag, int itemId, int count)
    {
        ArgumentNullException.ThrowIfNull(bag);
        var plan = new List<(Item Stack, int Take)>();
        var remaining = count;
        foreach (var held in bag.Where(item => item.ItemIndex == itemId && item.Stack > 0).OrderBy(item => item.ItemSlot))
        {
            if (remaining <= 0) break;
            var take = Math.Min(remaining, (int)held.Stack);
            plan.Add((held, take));
            remaining -= take;
        }
        return remaining > 0 ? null : plan;
    }
}
