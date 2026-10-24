namespace tPoseidon.Core.Game.Combat;

public sealed class TransientEntityIdAllocator
{
    public const ushort RangeStartInclusive = 0x3000;
    public const ushort RangeEndExclusive = 0x4000;

    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(10);

    private readonly object _gate = new();
    private readonly TimeSpan _lifetime;
    private readonly TimeSpan _cooldown;
    private readonly Func<DateTimeOffset> _now;
    private readonly Dictionary<ushort, DateTimeOffset> _liveExpiresAt = new();
    private readonly Dictionary<ushort, DateTimeOffset> _cooldownUntil = new();
    private ushort _nextScan = RangeStartInclusive;

    public TransientEntityIdAllocator()
        : this(DefaultLifetime, DefaultCooldown, () => DateTimeOffset.UtcNow)
    {
    }

    public TransientEntityIdAllocator(TimeSpan lifetime, TimeSpan cooldown, Func<DateTimeOffset> now)
    {
        _lifetime = lifetime;
        _cooldown = cooldown;
        _now = now;
    }

    public ushort Allocate()
    {
        lock (_gate)
        {
            var now = _now();
            ExpireLiveLocked(now);

            for (int attempted = 0; attempted < (RangeEndExclusive - RangeStartInclusive); attempted++)
            {
                var candidate = _nextScan;
                _nextScan = (ushort)(_nextScan + 1);
                if (_nextScan >= RangeEndExclusive) _nextScan = RangeStartInclusive;

                if (_liveExpiresAt.ContainsKey(candidate)) continue;
                if (_cooldownUntil.TryGetValue(candidate, out var cdEnd) && now < cdEnd) continue;

                _cooldownUntil.Remove(candidate);
                _liveExpiresAt[candidate] = now + _lifetime;
                return candidate;
            }

            throw new InvalidOperationException(
                $"TransientEntityIdAllocator exhausted: all {RangeEndExclusive - RangeStartInclusive} ids in range " +
                $"[0x{RangeStartInclusive:X4}..0x{RangeEndExclusive:X4}) are live or in cooldown.");
        }
    }

    public void Release(ushort id)
    {
        if (id < RangeStartInclusive || id >= RangeEndExclusive) return;
        lock (_gate)
        {
            if (_liveExpiresAt.Remove(id))
            {
                _cooldownUntil[id] = _now() + _cooldown;
            }
        }
    }

    private void ExpireLiveLocked(DateTimeOffset now)
    {
        List<ushort>? expired = null;
        foreach (var kv in _liveExpiresAt)
        {
            if (now >= kv.Value)
            {
                expired ??= new List<ushort>();
                expired.Add(kv.Key);
            }
        }
        if (expired is null) return;
        foreach (var id in expired)
        {
            _liveExpiresAt.Remove(id);
            _cooldownUntil[id] = now + _cooldown;
        }
    }
}
