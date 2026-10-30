using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public static class QuestRandomRewards
{
    public const int EveryTableRowType = 0;

    public static IReadOnlyList<RewardItem>? Draw(IReadOnlyList<int> tableIds, int rowType, IRandomResultTables tables, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(tableIds);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(next);
        if (tableIds.Count == 0) return Array.Empty<RewardItem>();
        var drawn = new List<RewardItem>(tableIds.Count);
        var chosen = rowType == EveryTableRowType ? tableIds : new[] { tableIds[next(tableIds.Count)] };
        foreach (var id in chosen)
        {
            if (tables.GetById(id) is not { Total: > 0 } table) return null;
            if (table.Pick(next(table.Total)) is not { } entry) return null;
            drawn.Add(new RewardItem(entry.ItemIndex, entry.Count));
        }
        return drawn;
    }
}
