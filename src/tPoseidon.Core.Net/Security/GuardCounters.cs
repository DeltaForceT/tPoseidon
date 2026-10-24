namespace tPoseidon.Core.Net.Security;

public enum GuardCounter
{
    Accepted,
    TrustedAccepted,
    RefusedBanned,
    RefusedServerFull,
    RefusedTooManyPending,
    RefusedAddressLimit,
    RefusedAddressRate,
    HandshakeTimeouts,
    LoginTimeouts,
    IdleTimeouts,
    PartialFrames,
    OversizedFrames,
    MalformedFrames,
    PacketFloods,
    LoginFailures,
    Lockouts,
    LockedOutAttempts,
    Bans,
    BanKicks,
    RequestsAllowed,
    RequestsRefused,
}

public sealed class GuardCounters
{
    private readonly long[] _values = new long[Enum.GetValues<GuardCounter>().Length];

    public long this[GuardCounter counter] => Interlocked.Read(ref _values[(int)counter]);

    public void Increment(GuardCounter counter) => Interlocked.Increment(ref _values[(int)counter]);

    public Dictionary<string, long> ToDictionary()
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var counter in Enum.GetValues<GuardCounter>())
        {
            result[Key(counter)] = this[counter];
        }

        return result;
    }

    public static string Key(GuardCounter counter)
    {
        var name = counter.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
