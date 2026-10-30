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

    public static IReadOnlyList<TimedItem> Registered(IEnumerable<Item> items) =>
        items.Where(TimedItemRules.IsTimed).Select(item => new TimedItem(item.ItemUid, item.ItemIndex, item.BreakedAt)).ToList();

    public static IReadOnlyList<TimedItem> Registered(IEnumerable<EquipUpgradeItem> rows) =>
        rows.Where(row => row.Action == EquipUpgradeAction.Insert && row.BreakedAt is not null)
            .Select(row => new TimedItem(row.ItemUid, row.ItemIndex, row.BreakedAt!.Value))
            .ToList();
}
