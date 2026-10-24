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

    public static byte CheckItem(bool inBag, bool equipped, bool busy, ItemParamEntry? definition)
    {
        if (!inBag) return equipped ? Equipped : NoItem;
        if (busy) return InvalidItemState;
        if (definition is null) return NoItemInfo;
        if (definition.MinLevel < MinItemLevel) return BelowLevel;
        if (definition.Class != EquipmentClass) return NotMeltable;
        return Ok;
    }

    public static int StatQuality(ItemParamEntry definition, IReadOnlyList<ItemProperty> properties, int minPercent, int maxPercent)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(properties);
        if (definition.HasRandomStats) return 0;
        var sum = 0.0;
        var counted = 0;
        for (var index = 0; index < definition.StatBonuses.Count; index++)
        {
            var baseValue = definition.StatBonuses[index];
            if (baseValue == 0) continue;
            var min = baseValue + baseValue * minPercent / PercentBase;
            var max = baseValue + baseValue * maxPercent / PercentBase;
            if (min <= StatQualityFloor || max <= StatQualityFloor) continue;
            var actual = properties
                .Where(property => property.Type == ItemPropertyTypes.Stat && property.Id == StatPropertyBase + index)
                .Select(property => (int?)property.Value)
                .FirstOrDefault() ?? baseValue;
            sum += min == max ? FullQuality : FullQuality * (actual - min) / (max - min);
            counted++;
        }
        return counted == 0 ? 0 : (int)Math.Truncate(sum / counted);
    }

    public static int Score(MeltingInfoEntry info, ItemParamEntry definition, Item item, int statQuality)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(item);
        return (int)Math.Truncate(
            definition.MinLevel * (double)info.ItemGradeRate
            + item.ItemSlot * (double)info.CompoundSlotRate
            + statQuality * (double)info.ItemValueRate
            + item.RefineLevel * (double)info.CraftGrade);
    }

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
