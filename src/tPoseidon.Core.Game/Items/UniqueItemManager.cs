using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public sealed class UniqueItemManager
{
    private readonly IReadOnlyList<UniqueItemCountEntry> _counts;
    private readonly HashSet<int> _legendItemIds;

    public UniqueItemManager(
        IEnumerable<UniqueItemCountEntry> counts,
        IEnumerable<LegendItemListEntry> legendItems)
    {
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentNullException.ThrowIfNull(legendItems);
        _counts = counts.ToList();
        _legendItemIds = new HashSet<int>(legendItems.Select(e => e.ItemId));
    }

    public int LegendItemCount => _legendItemIds.Count;

    public bool IsLegendItem(int itemId) => _legendItemIds.Contains(itemId);

    public float GetCountForLevel(int level)
    {
        foreach (var c in _counts)
        {
            if (level >= c.MinLevel && level <= c.MaxLevel) return c.Count;
        }
        return 0f;
    }
}
