using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public readonly record struct UniqueCountResult(uint Units, int? IssuedItemId);

public sealed class UniqueItemPool
{
    public const int RatioScale = 100;
    public const float LegendAmount = 1.0f;

    private readonly object _gate = new();
    private readonly Dictionary<int, LegendItemGroupEntry> _groups;
    private readonly Dictionary<uint, float> _counts = new();
    private readonly Dictionary<uint, Queue<int>> _issued = new();

    public UniqueItemPool(IEnumerable<LegendItemGroupEntry> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        _groups = new Dictionary<int, LegendItemGroupEntry>();
        foreach (var group in groups) _groups.TryAdd(group.MonsterId, group);
    }

    public int GroupCount => _groups.Count;

    public int? RollLegendKey(int monsterId, Func<int, int> rollUpTo)
    {
        ArgumentNullException.ThrowIfNull(rollUpTo);
        if (!_groups.TryGetValue(monsterId, out var group) || group.Items.Count == 0) return null;
        var roll = rollUpTo(RatioScale) + 1;
        var cumulative = 0f;
        foreach (var (itemId, ratio) in group.Items)
        {
            cumulative += ratio * RatioScale;
            if (roll <= cumulative) return itemId;
        }
        return null;
    }

    public UniqueCountResult AddCount(uint key, float amount)
    {
        lock (_gate)
        {
            var total = (_counts.TryGetValue(key, out var held) ? held : 0f) + amount;
            var units = (uint)Math.Floor(total);
            _counts[key] = total - units;
            int? issued = _issued.TryGetValue(key, out var queue) && queue.Count > 0 ? queue.Dequeue() : null;
            return new UniqueCountResult(units, issued);
        }
    }

    public void Issue(uint key, IEnumerable<int> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        lock (_gate)
        {
            if (!_issued.TryGetValue(key, out var queue))
            {
                queue = new Queue<int>();
                _issued[key] = queue;
            }
            foreach (var itemId in itemIds) queue.Enqueue(itemId);
        }
    }
}
