using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public static class TimedItemPeriods
{
    public static int MinutesOf(ItemParamEntry? definition, IReadOnlyDictionary<int, int> timerMinutes)
    {
        ArgumentNullException.ThrowIfNull(timerMinutes);
        return definition is { ExistType: ItemRechargeRules.TimeLimitedExistType } && timerMinutes.TryGetValue(definition.Id, out var minutes) && minutes > 0
            ? minutes
            : 0;
    }

    public static short? HoursOf(ItemParamEntry? definition, IReadOnlyDictionary<int, int> timerMinutes)
    {
        var minutes = MinutesOf(definition, timerMinutes);
        return minutes > 0 ? (short)Math.Min((minutes + ItemRechargeRules.MinutesPerHour - 1) / ItemRechargeRules.MinutesPerHour, short.MaxValue) : null;
    }

    public static int? PresentMinutes(ItemParamEntry? definition, int requestedHours, short? shopHours)
    {
        if (requestedHours > 0) return requestedHours * ItemRechargeRules.MinutesPerHour;
        return definition is { ExistType: ItemRechargeRules.TimeLimitedExistType } && shopHours is > 0 ? shopHours.Value * ItemRechargeRules.MinutesPerHour : null;
    }

    public static IReadOnlyList<TimedItem> Registered(IEnumerable<Item> items) =>
        items.Where(TimedItemRules.IsTimed).Select(item => new TimedItem(item.ItemUid, item.ItemIndex, item.BreakedAt)).ToList();

    public static IReadOnlyList<TimedItem> Registered(Item? item) => item is null ? [] : Registered([item]);

    public static IReadOnlyList<TimedItem> Registered(IEnumerable<EquipUpgradeItem> rows) =>
        rows.Where(row => row.Action == EquipUpgradeAction.Insert && row.BreakedAt is not null)
            .Select(row => new TimedItem(row.ItemUid, row.ItemIndex, row.BreakedAt!.Value))
            .ToList();
}
