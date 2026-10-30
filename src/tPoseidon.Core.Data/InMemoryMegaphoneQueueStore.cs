namespace tPoseidon.Core.Data;

public sealed class InMemoryMegaphoneQueueStore : IMegaphoneQueueStore
{
    private readonly object _gate = new();
    private readonly List<QueuedMegaphone> _rows = [];
    private long _nextCode = 1;

    public int Enqueue(ulong characterId, string message, MegaphoneRefund refund)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            var waiting = _rows.Count(row => MegaphoneFlags.IsPending(row.Flag));
            _rows.Add(new QueuedMegaphone(_nextCode++, characterId, message, MegaphoneKinds.Normal, MegaphoneFlags.Queued, refund));
            return waiting;
        }
    }

    public int EnqueueNotice(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            var waiting = _rows.Count(row => MegaphoneFlags.IsPending(row.Flag));
            _rows.Add(new QueuedMegaphone(_nextCode++, MegaphoneKinds.NoticeSender, message, MegaphoneKinds.Notice, MegaphoneFlags.Waiting, new MegaphoneRefund(0, 0)));
            return waiting;
        }
    }

    public QueuedMegaphone? NextPending()
    {
        lock (_gate)
        {
            return _rows.FirstOrDefault(row => MegaphoneFlags.IsPending(row.Flag));
        }
    }

    public bool MarkDelivered(long code)
    {
        lock (_gate)
        {
            var index = _rows.FindIndex(row => row.Code == code && MegaphoneFlags.IsPending(row.Flag));
            if (index < 0) return false;
            _rows[index] = _rows[index] with { Flag = MegaphoneFlags.Delivered };
            return true;
        }
    }

    public IReadOnlyList<QueuedMegaphone> Interrupted()
    {
        lock (_gate)
        {
            return _rows.Where(row => row.Flag == MegaphoneFlags.Queued).ToList();
        }
    }

    public void Settle(long code, byte flag)
    {
        lock (_gate)
        {
            var index = _rows.FindIndex(row => row.Code == code);
            if (index >= 0) _rows[index] = _rows[index] with { Flag = flag };
        }
    }

    public IReadOnlyList<QueuedMegaphone> All()
    {
        lock (_gate)
        {
            return _rows.ToList();
        }
    }
}
