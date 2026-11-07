using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public static class MeltingRules
{
    public const byte Ok = 0;
    public const byte InvalidState = 1;
    public const byte NoItem = 2;
    public const byte InvalidItemState = 3;
    public const byte Equipped = 4;
    public const byte NoMeltingInfo = 5;
    public const byte NoItemInfo = 6;
    public const byte OverWeight = 7;
    public const byte NotEnoughGelder = 8;
    public const byte BelowLevel = 9;
    public const byte NoBagSpace = 10;
    public const byte Unknown = 11;
    public const byte NotMeltable = 12;

    public const int MaxItems = 5;
    public const int MinItemLevel = 30;
    public const int EquipmentClass = 2;
    public const int StatPropertyBase = 10000;
    public const int StatQualityFloor = 10;
    public const double FullQuality = 100.0;
    public const int PercentBase = 100;
    public const int CashItemOption = 0x40000;

    public static byte CheckItem(bool inBag, bool equipped, bool busy, ItemParamEntry? definition)
    {
        if (!inBag) return equipped ? Equipped : NoItem;
        if (busy) return InvalidItemState;
        if (definition is null) return NoItemInfo;
        if (definition.MinLevel < MinItemLevel) return BelowLevel;
        if (definition.Class != EquipmentClass) return NotMeltable;
        return Ok;
    }

    public static int StatQuality(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties, MeltingReference reference)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(reference);
        reference.RandomStats.TryGetValue(definition.Id, out var randomRow);
        var sum = 0f;
        var counted = 0;
        for (var index = 0; index < ItemPropertyRules.BaseStatCount; index++)
        {
            var baseValue = index < definition.StatBonuses.Count ? definition.StatBonuses[index] : 0;
            var key = StatPropertyBase + index;
            int min, max, actual;
            if (definition.HasRandomStats)
            {
                if (randomRow is null || index >= randomRow.Stats.Count || randomRow.Stats[index] is not { } range) continue;
                actual = StoredStat(properties, key, 0);
                if (actual == 0) continue;
                min = range.Min;
                max = range.Max;
            }
            else
            {
                if (baseValue == 0) continue;
                min = baseValue + baseValue * reference.RandomStatMinPercent / PercentBase;
                max = baseValue + baseValue * reference.RandomStatMaxPercent / PercentBase;
                actual = StoredStat(properties, key, baseValue);
            }
            if (min <= StatQualityFloor || max <= StatQualityFloor) continue;
            Accumulate(ref sum, ref counted, min, max, actual);
        }
        if (definition.SubType == ItemParamEntry.GunSubType)
        {
            int min, max, actual;
            if (definition.HasRandomStats)
            {
                if (randomRow is null || ItemPropertyRules.GunApId - StatPropertyBase >= randomRow.Stats.Count || randomRow.Stats[ItemPropertyRules.GunApId - StatPropertyBase] is not { } range) return Average(sum, counted);
                min = range.Min;
                max = range.Max;
                actual = StoredStat(properties, ItemPropertyRules.GunApId, 0);
            }
            else
            {
                min = definition.Ap + definition.Ap * reference.RandomStatMinPercent / PercentBase;
                max = definition.Ap + definition.Ap * reference.RandomStatMaxPercent / PercentBase;
                actual = StoredStat(properties, ItemPropertyRules.GunApId, definition.Ap);
            }
            if ((min > StatQualityFloor || max > StatQualityFloor) && actual > 0) Accumulate(ref sum, ref counted, min, max, actual);
        }
        return Average(sum, counted);
    }

    public static int CompoundValue(Item item, IReadOnlyList<ItemProperty> properties, IReadOnlyDictionary<int, MaturingCompoundRow> materials)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(materials);
        var sum = 0f;
        var counted = 0;
        for (var slot = 0; slot < item.ItemSlot; slot++)
        {
            var found = properties.Where(property => property.Type == slot).Select(property => (ItemProperty?)property).FirstOrDefault();
            if (found is not { } mix || !materials.TryGetValue((int)mix.Extra, out var row) || row.MaxAbility == row.MinAbility) continue;
            counted++;
            sum = (float)(sum + (double)(row.MaxAbility - mix.Value) * FullQuality / (row.MaxAbility - row.MinAbility));
        }
        return counted == 0 ? 0 : (int)Math.Truncate((double)sum / counted);
    }

    public static bool IsCashItem(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(properties);
        var options = definition.Class == EquipmentClass
            ? properties.Where(property => property.Type == ItemPropertyTypes.Options).Select(property => (uint?)property.Extra).FirstOrDefault()
            : null;
        return ((long)(options ?? (uint)definition.Options) & CashItemOption) != 0;
    }

    public static int Score(MeltingInfoEntry info, ItemParamEntry definition, Item item, IReadOnlyList<ItemProperty> properties, MeltingReference reference)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(reference);
        var score = (float)((double)definition.RefineIndex * info.ItemGradeRate);
        score = (float)(score + (double)item.ItemSlot * info.CompoundSlotRate);
        score = (float)(score + (double)CompoundValue(item, properties, reference.Materials) * info.CompoundValueRate);
        score = (float)(score + (double)StatQuality(definition, properties, reference) * info.ItemValueRate);
        score = (float)(score + (double)item.RefineLevel * info.CraftGrade);
        if (IsCashItem(definition, properties)) score = (float)(score + (double)(ushort)info.CmOption);
        return (int)Math.Truncate((double)score);
    }

    private static int StoredStat(IReadOnlyList<ItemProperty> properties, int key, int fallback)
    {
        var stored = properties
            .Where(property => property.Type == ItemPropertyTypes.Stat && property.Id == key)
            .Select(property => (int?)property.Value)
            .FirstOrDefault() ?? 0;
        return stored != 0 ? stored : fallback;
    }

    private static void Accumulate(ref float sum, ref int counted, int min, int max, int actual)
    {
        var quality = min == max ? FullQuality : FullQuality / (max - min) * (actual - min);
        sum = (float)(sum + quality);
        counted++;
    }

    private static int Average(float sum, int counted) =>
        counted == 0 ? 0 : (int)Math.Truncate((double)(float)((double)sum / counted));

    public static MeltingDropEntry? Band(IReadOnlyList<MeltingDropEntry> drops, int total)
    {
        ArgumentNullException.ThrowIfNull(drops);
        return drops.FirstOrDefault(drop => drop.MinValue <= total && total <= drop.MaxValue);
    }

    public static long Fee(MeltingDropEntry band, int itemCount)
    {
        ArgumentNullException.ThrowIfNull(band);
        return (long)band.MeltingCost * itemCount;
    }
}

public sealed record MeltingReference(
    int RandomStatMinPercent,
    int RandomStatMaxPercent,
    IReadOnlyDictionary<int, RandomStatRow> RandomStats,
    IReadOnlyDictionary<int, MaturingCompoundRow> Materials)
{
    public static MeltingReference Of(MeltingTables melting, ItemPropertyTables properties, GrowthTables growth)
    {
        ArgumentNullException.ThrowIfNull(melting);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(growth);
        return new MeltingReference(melting.RandomStatMinPercent, melting.RandomStatMaxPercent, properties.RandomStats, growth.Materials);
    }
}
