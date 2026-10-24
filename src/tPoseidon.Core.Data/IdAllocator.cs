namespace tPoseidon.Core.Data;

public sealed class IdAllocator
{
    private readonly object _gate = new();
    private readonly Stack<ushort> _freeList = new();
    private ushort _nextId;
    private readonly ushort _firstId;
    private readonly ushort _lastId;

        public IdAllocator(ushort firstId = 0x0100, ushort lastId = 0xFFFE)
    {
        if (firstId > lastId)
        {
            throw new ArgumentException(
                $"firstId ({firstId}) must be ≤ lastId ({lastId}).");
        }
        _firstId = firstId;
        _lastId = lastId;
        _nextId = firstId;
    }

        public ushort NewId()
    {
        lock (_gate)
        {
            while (_freeList.Count > 0)
            {
                var id = _freeList.Pop();
                if (!_reserved.Contains(id)) return id;
            }
            while (true)
            {
                if (_nextId > _lastId)
                {
                    throw new InvalidOperationException(
                        $"IdAllocator exhausted: range [{_firstId:X4}..{_lastId:X4}] all in use.");
                }
                var candidate = _nextId++;
                if (!_reserved.Contains(candidate)) return candidate;
            }
        }
    }

    private readonly HashSet<ushort> _reserved = new();

    public void Reserve(ushort id)
    {
        if (id < _firstId || id > _lastId) return;
        lock (_gate) _reserved.Add(id);
    }

        public void Release(ushort id)
    {
        lock (_gate)
        {
            if (id < _firstId || id > _lastId)
            {
                return;
            }
            if (!_freeList.Contains(id))
            {
                _freeList.Push(id);
            }
        }
    }

        public int InFlightCount
    {
        get
        {
            lock (_gate)
            {
                var allocated = _nextId - _firstId;
                return allocated - _freeList.Count;
            }
        }
    }

        public ushort NextIdHighWaterMark
    {
        get
        {
            lock (_gate) { return _nextId; }
        }
    }

        public void AdvanceHighWaterTo(ushort id)
    {
        lock (_gate)
        {
            if (id > _nextId) _nextId = id;
        }
    }
}
