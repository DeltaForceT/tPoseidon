using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using tPoseidon.DrillEgg.Connectors;

namespace tPoseidon.DrillEgg.Web;

internal sealed record PlayerSession(string Token, string FormToken, GachaPlayer Player, DateTimeOffset ExpiresAt);

internal sealed class PlayerSessions
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
    private const int TokenBytes = 32;

    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;

    public PlayerSessions(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public PlayerSession Open(GachaPlayer player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var session = new PlayerSession(NewToken(), NewToken(), player, _clock.GetUtcNow() + Lifetime);
        _sessions[session.Token] = session;
        return session;
    }

    public PlayerSession? Find(string? token)
    {
        if (string.IsNullOrEmpty(token) || !_sessions.TryGetValue(token, out var session)) return null;
        var now = _clock.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }
        var renewed = session with { ExpiresAt = now + Lifetime };
        _sessions.TryUpdate(token, renewed, session);
        return renewed;
    }

    public PlayerSession? Authorize(string? token, string? formToken)
    {
        if (Find(token) is not { } session || formToken is null) return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(session.FormToken), Encoding.ASCII.GetBytes(formToken)) ? session : null;
    }

    public void Close(string? token)
    {
        if (!string.IsNullOrEmpty(token)) _sessions.TryRemove(token, out _);
    }

    public int Sweep()
    {
        var now = _clock.GetUtcNow();
        var removed = 0;
        foreach (var (token, session) in _sessions)
        {
            if (session.ExpiresAt <= now && _sessions.TryRemove(token, out _)) removed++;
        }
        return removed;
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();
}

internal sealed class SignInThrottle
{
    public const int MaxFailures = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<IPAddress, (int Failures, DateTimeOffset Since)> _failures = new();
    private readonly TimeProvider _clock;

    public SignInThrottle(TimeProvider clock)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public bool IsBlocked(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!_failures.TryGetValue(address, out var entry)) return false;
        if (_clock.GetUtcNow() - entry.Since >= Window)
        {
            _failures.TryRemove(address, out _);
            return false;
        }
        return entry.Failures >= MaxFailures;
    }

    public void Failed(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var now = _clock.GetUtcNow();
        _failures.AddOrUpdate(address, (1, now), (_, entry) => now - entry.Since >= Window ? (1, now) : (entry.Failures + 1, entry.Since));
    }

    public void Succeeded(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        _failures.TryRemove(address, out _);
    }
}
