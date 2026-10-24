using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Items;

public sealed record TimedItem(ulong ItemUid, int ItemIndex, DateTimeOffset ExpiresAt);

public readonly record struct TimedItemEvent(TimedItem Item, int MinutesLeft, bool Expired);

public static class TimedItemRules
{
    public const int NoticeWindowMinutes = 1440;
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan ShortestListed = TimeSpan.FromMinutes(1);

    private static readonly int[] BandFloors = { 60, 10, 1, 0 };

    public static bool IsTimed(Item item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.BreakedAt > Item.NeverBreaks;
    }

    public static bool HasExpired(Item item, DateTimeOffset now) => IsTimed(item) && item.BreakedAt <= now;

    public static IReadOnlyList<TimedItem> Running(IEnumerable<Item> items, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items
            .Where(item => IsTimed(item) && item.BreakedAt - now >= ShortestListed)
            .Select(item => new TimedItem(item.ItemUid, item.ItemIndex, item.BreakedAt))
            .ToList();
    }

    public static int MinutesLeft(DateTimeOffset expiresAt, DateTimeOffset now) =>
        (int)Math.Max(0, Math.Ceiling((expiresAt - now).TotalMinutes));

    public static int? NoticeBand(int minutesLeft)
    {
        if (minutesLeft > NoticeWindowMinutes || minutesLeft <= 0) return null;
        for (var band = 0; band < BandFloors.Length; band++)
        {
            if (minutesLeft > BandFloors[band]) return band;
        }
        return null;
    }
}

public sealed class TimedItemWatch
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, (TimedItem Item, int? Band)> _items = new();

    public int Count
    {
        get { lock (_gate) return _items.Count; }
    }

    public void Track(TimedItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_gate) _items[item.ItemUid] = (item, null);
    }

    public void Forget(ulong itemUid)
    {
        lock (_gate) _items.Remove(itemUid);
    }

    public void Clear()
    {
        lock (_gate) _items.Clear();
    }

    public IReadOnlyList<TimedItemEvent> Tick(DateTimeOffset now)
    {
        var events = new List<TimedItemEvent>();
        lock (_gate)
        {
            foreach (var (uid, (item, band)) in _items.ToList())
            {
                var left = TimedItemRules.MinutesLeft(item.ExpiresAt, now);
                if (left == 0)
                {
                    _items.Remove(uid);
                    events.Add(new TimedItemEvent(item, 0, true));
                    continue;
                }
                var current = TimedItemRules.NoticeBand(left);
                if (current is null || current == band) continue;
                _items[uid] = (item, current);
                events.Add(new TimedItemEvent(item, left, false));
            }
        }
        return events;
    }
}
