using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

namespace tPoseidon.WebShop;

public sealed record ShopSession(string Token, string FormToken, uint AccountId, string Username, int World, IPAddress RemoteIp, DateTimeOffset ExpiresAt);

public sealed class ShopSessions
{
    public const int TokenBytes = 32;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, ShopSession> _sessions = new(StringComparer.Ordinal);

    public int Count => _sessions.Count;

    public ShopSession Open(uint accountId, string username, int world, IPAddress remoteIp, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(remoteIp);
        Prune(now);
        var session = new ShopSession(NewToken(), NewToken(), accountId, username, world, ShopGate.Normalize(remoteIp), now + Lifetime);
        _sessions[session.Token] = session;
        return session;
    }

    public ShopSession? Find(string? token, IPAddress remoteIp, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(remoteIp);
        if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token, out var session)) return null;
        if (session.ExpiresAt <= now || !session.RemoteIp.Equals(ShopGate.Normalize(remoteIp)))
        {
            _sessions.TryRemove(token, out _);
            return null;
        }
        var renewed = session with { ExpiresAt = now + Lifetime };
        _sessions.TryUpdate(token, renewed, session);
        return renewed;
    }

    public void Close(string? token)
    {
        if (!string.IsNullOrEmpty(token)) _sessions.TryRemove(token, out _);
    }

    public void Prune(DateTimeOffset now)
    {
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAt <= now) _sessions.TryRemove(pair.Key, out _);
        }
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes));
}
