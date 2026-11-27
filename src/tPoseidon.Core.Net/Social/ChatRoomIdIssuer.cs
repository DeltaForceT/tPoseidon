namespace tPoseidon.Core.Net.Social;

public sealed class ChatRoomIdIssuer
{
    private readonly object _gate = new();
    private readonly HashSet<ushort> _used = new();
    private readonly ushort _reserved;
    private ushort _last;

    public ChatRoomIdIssuer(ushort reserved)
    {
        _reserved = reserved;
        _last = reserved;
    }

    public ushort? Issue()
    {
        lock (_gate)
        {
            var start = _last;
            var candidate = start;
            do
            {
                candidate++;
                if (candidate == 0 || candidate == _reserved || _used.Contains(candidate)) continue;
                _used.Add(candidate);
                _last = candidate;
                return candidate;
            }
            while (candidate != start);
            return null;
        }
    }

    public void Release(ushort id)
    {
        lock (_gate) _used.Remove(id);
    }
}
