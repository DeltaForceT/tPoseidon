namespace tPoseidon.Core.Net.World;

public sealed class EntityIdManager
{
    public const ushort ReservedNull = 0x0000;
    public const ushort TransientRangeStart = 0x3000;
    public const ushort TransientRangeEndExclusive = 0x4000;
    public const ushort UsableStart = 0x4000;
    public const ushort UsableEndExclusive = 0x7FFF;

    private readonly object _gate = new();
    private readonly HashSet<ushort> _inUse = new();
    private ushort _nextScan;

    public EntityIdManager(ushort? startOffset = null)
    {
        var seed = startOffset ?? UsableStart;
        if (seed < UsableStart || seed >= UsableEndExclusive)
        {
            seed = UsableStart;
        }
        _nextScan = seed;
    }

    public ushort Allocate()
    {
        lock (_gate)
        {
            for (int attempted = 0; attempted < (UsableEndExclusive - UsableStart); attempted++)
            {
                var candidate = _nextScan;
                Advance();

                if (candidate >= TransientRangeStart && candidate < TransientRangeEndExclusive)
                {
                    continue;
                }
                if (_inUse.Contains(candidate))
                {
                    continue;
                }
                _inUse.Add(candidate);
                return candidate;
            }
            throw new InvalidOperationException(
                $"EntityIdManager exhausted: all usable ids in [0x{UsableStart:X4}..0x{UsableEndExclusive:X4}) " +
                $"excluding transient [0x{TransientRangeStart:X4}..0x{TransientRangeEndExclusive:X4}) are live.");
        }
    }

    public void Release(ushort id)
    {
        if (id == ReservedNull) return;
        lock (_gate)
        {
            _inUse.Remove(id);
        }
    }

    public int LiveCount
    {
        get { lock (_gate) return _inUse.Count; }
    }

    private void Advance()
    {
        var next = (ushort)(_nextScan + 1);
        if (next >= UsableEndExclusive) next = UsableStart;
        _nextScan = next;
    }
}
