namespace tPoseidon.Core.Game.MyCamp;

public readonly record struct MyCampTimerNotice(ulong ItemUid, ushort ItemIndex, uint Minutes);

public static class MyCampTimerRules
{
    public const byte TimedItemFlag = 1;
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    public static readonly IReadOnlyList<int> ThresholdMinutes = [1440, 60, 5, 0];

    public static DateTimeOffset? LimitOf(byte itemFlag, DateTimeOffset? useDate, short useTimeHours) =>
        itemFlag == TimedItemFlag && useDate is { } start ? start + TimeSpan.FromHours(useTimeHours) : null;

    public static int Bucket(TimeSpan remaining)
    {
        if (remaining > TimeSpan.FromMinutes(ThresholdMinutes[0])) return -1;
        for (var i = 1; i < ThresholdMinutes.Count; i++)
        {
            if (TimeSpan.FromMinutes(ThresholdMinutes[i]) < remaining) return i - 1;
        }
        return ThresholdMinutes.Count - 1;
    }
}

public sealed class MyCampTimerTracker
{
    private sealed class Entry(ushort itemIndex, DateTimeOffset limit)
    {
        public ushort ItemIndex { get; } = itemIndex;
        public DateTimeOffset Limit { get; } = limit;
        public bool[] Notified { get; } = new bool[MyCampTimerRules.ThresholdMinutes.Count];
    }

    private readonly Dictionary<ulong, Entry> _entries = new();
    private DateTimeOffset _lastCheck;

    public int Count => _entries.Count;

    public void Load(IEnumerable<(ulong ItemUid, ushort ItemIndex, DateTimeOffset Limit)> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var (uid, index, limit) in items) _entries.TryAdd(uid, new Entry(index, limit));
    }

    public IReadOnlyList<MyCampTimerNotice> Tick(DateTimeOffset now)
    {
        var elapsed = now - _lastCheck;
        if (elapsed >= TimeSpan.Zero && elapsed <= MyCampTimerRules.CheckInterval) return [];
        _lastCheck = now;
        var notices = new List<MyCampTimerNotice>();
        foreach (var (uid, entry) in _entries.ToArray())
        {
            var remaining = entry.Limit > now ? entry.Limit - now : TimeSpan.Zero;
            var bucket = MyCampTimerRules.Bucket(remaining);
            if (bucket < 0) continue;
            var minutes = MyCampTimerRules.ThresholdMinutes[bucket];
            if (!entry.Notified[bucket])
            {
                notices.Add(new MyCampTimerNotice(uid, entry.ItemIndex, (uint)minutes));
                entry.Notified[bucket] = true;
            }
            if (minutes < 1) _entries.Remove(uid);
        }
        return notices;
    }
}
