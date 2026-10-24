using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Warp;

public sealed class WarpManager
{
    private readonly ConcurrentDictionary<uint, PendingWarpEntry> _byToken = new();
    private readonly ConcurrentDictionary<ulong, DateTime> _landingClaims = new();
    private readonly TimeSpan _ttl;
    private long _nextToken = 0x00001000;

    public WarpManager(TimeSpan? ttl = null)
    {
        _ttl = ttl ?? TimeSpan.FromSeconds(30);
    }

    public uint Begin(PendingWarp warp)
    {
        TryBegin(warp, out var token);
        return token;
    }

    public bool TryBegin(PendingWarp warp, out uint token)
    {
        foreach (var kv in _byToken)
        {
            if (kv.Value.Warp.CharacterId == warp.CharacterId
                && kv.Value.Expires >= DateTime.UtcNow)
            {
                token = kv.Key;
                return false;
            }
        }

        token = (uint)Interlocked.Increment(ref _nextToken);
        _byToken[token] = new PendingWarpEntry(warp, DateTime.UtcNow + _ttl);
        return true;
    }

    public PendingWarp? ConsumeByToken(uint token)
    {
        if (token == 0) return null;
        if (!_byToken.TryRemove(token, out var entry)) return null;
        return entry.Expires < DateTime.UtcNow ? null : entry.Warp;
    }

    public PendingWarp? ConsumeByCharId(ulong characterId)
    {
        foreach (var kv in _byToken)
        {
            if (kv.Value.Warp.CharacterId == characterId)
            {
                if (_byToken.TryRemove(kv.Key, out var entry))
                    return entry.Expires < DateTime.UtcNow ? null : entry.Warp;
                return null;
            }
        }
        return null;
    }

    public bool TryClaimLanding(ulong characterId)
    {
        var now = DateTime.UtcNow;
        if (_landingClaims.TryAdd(characterId, now + _ttl))
        {
            return true;
        }

        if (_landingClaims.TryGetValue(characterId, out var expires) && expires < now
            && _landingClaims.TryUpdate(characterId, now + _ttl, expires))
        {
            return true;
        }

        return false;
    }

    public void ReleaseLandingClaim(ulong characterId) =>
        _landingClaims.TryRemove(characterId, out _);

    public int Sweep()
    {
        var now = DateTime.UtcNow;
        var dropped = 0;
        foreach (var kv in _byToken)
        {
            if (kv.Value.Expires < now && _byToken.TryRemove(kv.Key, out _)) dropped++;
        }
        foreach (var kv in _landingClaims)
        {
            if (kv.Value < now && _landingClaims.TryRemove(kv.Key, out _)) dropped++;
        }
        return dropped;
    }

    public int PendingCount => _byToken.Count;

    private readonly record struct PendingWarpEntry(PendingWarp Warp, DateTime Expires);
}

public sealed record PendingWarp
{
    public required ulong CharacterId { get; init; }
    public required ushort SourceMapId { get; init; }
    public required ushort DestMapId { get; init; }
    public required ushort SpawnX { get; init; }
    public required ushort SpawnY { get; init; }
    public byte ChangeType { get; init; }
}
