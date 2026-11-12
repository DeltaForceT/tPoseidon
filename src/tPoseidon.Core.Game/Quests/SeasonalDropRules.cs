using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public readonly record struct SeasonalTableItem(int TableId, NpcSeason Season, DropItem Item);

public readonly record struct SeasonalRowItem(int RowId, NpcSeason Season, DropItem Item);

public static class SeasonalDropRules
{
    public const int FirstDailyQuest = 6000;
    public const int DailyRegions = 13;
    public const NpcSeason DataSeason = NpcSeason.Summer;

    public static readonly IReadOnlyList<SeasonalTableItem> TableItems =
    [
        new(270, NpcSeason.Spring, new DropItem(10512, 0.5f, 1)),
        new(271, NpcSeason.Spring, new DropItem(10512, 0.5f, 1)),
        new(272, NpcSeason.Spring, new DropItem(10515, 0.5f, 1)),
        new(273, NpcSeason.Spring, new DropItem(10515, 0.5f, 1)),
        new(274, NpcSeason.Spring, new DropItem(10518, 0.5f, 1)),
        new(275, NpcSeason.Spring, new DropItem(10518, 0.5f, 1)),
    ];

    public static readonly IReadOnlyList<SeasonalRowItem> RowItems =
    [
        new(108, NpcSeason.Summer, new DropItem(621, 0.2f, 1)),
        new(148, NpcSeason.Summer, new DropItem(623, 0.15f, 1)),
        .. new[] { 28, 145, 146, 147, 148, 149, 150, 151, 190, 191, 192, 193, 194, 195 }
            .Select(row => new SeasonalRowItem(row, NpcSeason.Spring, new DropItem(10518, 0.005f, 1))),
    ];

    public static int DailyQuestOf(NpcSeason season, int region) => FirstDailyQuest + (int)season * DailyRegions + region;

    public static IReadOnlyDictionary<int, int> DailyItemSwaps(IQuestInfoLookup quests, NpcSeason current)
    {
        ArgumentNullException.ThrowIfNull(quests);
        var swaps = new Dictionary<int, int>();
        if (current == DataSeason) return swaps;
        for (var region = 0; region < DailyRegions; region++)
        {
            var from = quests.GetById(DailyQuestOf(DataSeason, region))?.SrcItem0 ?? 0;
            var to = quests.GetById(DailyQuestOf(current, region))?.SrcItem0 ?? 0;
            if (from > 0 && to > 0 && from != to) swaps.TryAdd(from, to);
        }
        return swaps;
    }

    public static IReadOnlyList<BossGiveItemEntry> ApplyToBossGiveItems(IReadOnlyList<BossGiveItemEntry> entries, IReadOnlyDictionary<int, int> swaps)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(swaps);
        if (swaps.Count == 0) return entries;
        return entries
            .Select(entry => entry.Slots.Any(slot => swaps.ContainsKey(slot.ItemId))
                ? entry with { Slots = entry.Slots.Select(slot => swaps.TryGetValue(slot.ItemId, out var to) ? slot with { ItemId = to } : slot).ToArray() }
                : entry)
            .ToArray();
    }

    public static IReadOnlyList<MonItemTableDropEntry> ApplyToTables(IReadOnlyList<MonItemTableDropEntry> tables, NpcSeason current)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return tables.Select(table =>
        {
            var slots = table.Slots.ToList();
            foreach (var seasonal in TableItems.Where(item => item.TableId == table.Id))
            {
                var at = slots.FindIndex(slot => slot.ItemId == seasonal.Item.ItemId);
                if (seasonal.Season == current && at < 0)
                {
                    var free = slots.FindIndex(slot => slot.ItemId == 0 || slot.Rate <= 0);
                    if (free >= 0) slots[free] = seasonal.Item;
                    else slots.Add(seasonal.Item);
                }
                else if (seasonal.Season != current && at >= 0)
                {
                    slots[at] = default;
                }
            }
            return slots.SequenceEqual(table.Slots) ? table : table with { Slots = slots };
        }).ToArray();
    }

    public static IReadOnlyList<MonItemDropInfoEntry> ApplyToRows(IReadOnlyList<MonItemDropInfoEntry> rows, NpcSeason current)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.Select(row =>
        {
            var items = row.Items.ToList();
            foreach (var seasonal in RowItems.Where(item => item.RowId == row.Id))
            {
                var at = items.FindIndex(item => item.ItemId == seasonal.Item.ItemId);
                if (seasonal.Season == current && at < 0) items.Add(seasonal.Item);
                else if (seasonal.Season != current && at >= 0) items.RemoveAt(at);
            }
            return items.SequenceEqual(row.Items) ? row : row with { Items = items };
        }).ToArray();
    }
}
