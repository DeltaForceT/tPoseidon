namespace tPoseidon.Core.Game.Combat;

public sealed class EngagementList
{
    public const int PlayerCapacity = 10;
    public static readonly TimeSpan TimeToLive = TimeSpan.FromMilliseconds(5000);

    private readonly object _gate = new();
    private readonly List<Entry> _entries = new();
    private readonly int _capacity;

    public EngagementList(int capacity = PlayerCapacity)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity), "The list holds at least one target.");
        _capacity = capacity;
    }

    public bool Contains(ushort targetEntityId)
    {
        lock (_gate)
        {
            return IndexOf(targetEntityId) >= 0;
        }
    }

    public ushort? Refresh(ushort targetEntityId, DateTimeOffset now)
    {
        lock (_gate)
        {
            var at = IndexOf(targetEntityId);
            if (at >= 0)
            {
                _entries[at] = new Entry(targetEntityId, now + TimeToLive);
                return null;
            }

            ushort? evicted = null;
            if (_entries.Count >= _capacity)
            {
                evicted = _entries[0].TargetEntityId;
                _entries.RemoveAt(0);
            }
            _entries.Add(new Entry(targetEntityId, now + TimeToLive));
            return evicted;
        }
    }

    public IReadOnlyList<ushort> TakeExpired(DateTimeOffset now)
    {
        lock (_gate)
        {
            List<ushort>? expired = null;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (now < _entries[i].ExpiresAt) continue;
                (expired ??= new List<ushort>()).Add(_entries[i].TargetEntityId);
                _entries.RemoveAt(i);
            }
            if (expired is null) return Array.Empty<ushort>();
            expired.Reverse();
            return expired;
        }
    }

    public IReadOnlyList<ushort> TakeAll()
    {
        lock (_gate)
        {
            var all = new ushort[_entries.Count];
            for (var i = 0; i < all.Length; i++) all[i] = _entries[i].TargetEntityId;
            _entries.Clear();
            return all;
        }
    }

    private int IndexOf(ushort targetEntityId)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (_entries[i].TargetEntityId == targetEntityId) return i;
        }
        return -1;
    }

    private readonly record struct Entry(ushort TargetEntityId, DateTimeOffset ExpiresAt);
}
