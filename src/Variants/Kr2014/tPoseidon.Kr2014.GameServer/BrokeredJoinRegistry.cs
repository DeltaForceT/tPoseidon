using System.Collections.Concurrent;

namespace tPoseidon.Kr2014.GameServer;

public sealed record BrokeredJoin(
    ulong CharUid,
    ushort FromZone,
    ushort ToZone,
    uint Token,
    DateTimeOffset PreparedAtUtc,
    byte ChangeZoneType = 0,
    ushort StartX = 0,
    ushort StartY = 0);

public sealed class BrokeredJoinRegistry
{
    private readonly ConcurrentDictionary<ulong, BrokeredJoin> _pending = new();
    private readonly TimeSpan _ttl;

    public BrokeredJoinRegistry(TimeSpan ttl)
    {
        _ttl = ttl;
    }

    public void Register(BrokeredJoin join) => _pending[join.CharUid] = join;

    public bool TryConsume(ulong charUid, out BrokeredJoin join)
    {
        if (_pending.TryRemove(charUid, out join!))
        {
            if (DateTimeOffset.UtcNow - join.PreparedAtUtc <= _ttl)
            {
                return true;
            }
        }
        join = default!;
        return false;
    }

}
