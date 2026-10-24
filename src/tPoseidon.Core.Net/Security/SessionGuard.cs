using System.Globalization;
using System.Net;

namespace tPoseidon.Core.Net.Security;

public sealed class SessionGuard
{
    public static readonly SessionGuard None = new(null, IPAddress.None, null, trusted: true, 0);

    private readonly ConnectionGuard? _owner;
    private int _pending;
    private int _released;
    private int _aborted;
    private int _sawFrame;
    private int _authenticated;
    private long _lastFrameAt;
    private long _partialSince;
    private Action? _abort;
    private TokenBucket? _packets;
    private Dictionary<ushort, TokenBucket>? _opcodes;
    private TokenBucket? _otherOpcodes;

    internal SessionGuard(ConnectionGuard? owner, IPAddress address, AddressRecord? record, bool trusted, long acceptedAt)
    {
        _owner = owner;
        Address = address;
        Record = record;
        IsTrusted = trusted;
        AcceptedAt = acceptedAt;
        _lastFrameAt = acceptedAt;
        _pending = owner is not null && !trusted ? 1 : 0;
    }

    public IPAddress Address { get; }

    public bool IsTrusted { get; }

    public bool IsGuarded => _owner is not null && !IsTrusted;

    public bool IsAuthenticated => Volatile.Read(ref _authenticated) == 1;

    public bool HasSentFrame => Volatile.Read(ref _sawFrame) == 1;

    public int MaxFrameBytes => IsGuarded ? _owner!.Settings.MaxFrameBytes : ushort.MaxValue;

    internal AddressRecord? Record { get; }

    internal long AcceptedAt { get; }

    internal long LastFrameAt => Interlocked.Read(ref _lastFrameAt);

    internal long PartialSince => Interlocked.Read(ref _partialSince);

    public void Bind(Action abort)
    {
        ArgumentNullException.ThrowIfNull(abort);
        if (_owner is not null)
        {
            _abort = abort;
        }
    }

    public void FrameAccepted()
    {
        if (!IsGuarded)
        {
            return;
        }

        Interlocked.Exchange(ref _lastFrameAt, _owner!.Now());
        Interlocked.Exchange(ref _sawFrame, 1);
        if (!_owner.ExpectsLogin)
        {
            LeavePending();
        }
    }

    public bool AllowPacket(ushort opcode)
    {
        if (!IsGuarded)
        {
            return true;
        }

        var owner = _owner!;
        var now = owner.Now();
        _packets ??= owner.NewPacketBucket(now);
        if (!_packets.TryTake(now))
        {
            owner.Cut(this, GuardIncident.PacketFlood, GuardCounter.PacketFloods, string.Create(CultureInfo.InvariantCulture,
                $"more than {owner.Settings.PacketBurst} frames at once or {owner.Settings.PacketsPerSecond:0.##}/s, closed"));
            return false;
        }

        var bucket = OpcodeBucket(owner, opcode, now);
        if (!bucket.TryTake(now))
        {
            owner.Cut(this, GuardIncident.PacketFlood, GuardCounter.PacketFloods, string.Create(CultureInfo.InvariantCulture,
                $"opcode 0x{opcode:X4} past its limit of {bucket.Capacity:0} at once, closed"));
            return false;
        }

        return true;
    }

    public void Reject(GuardIncident incident, string detail)
    {
        if (!IsGuarded)
        {
            return;
        }

        var counter = incident switch
        {
            GuardIncident.OversizedFrame => GuardCounter.OversizedFrames,
            GuardIncident.MalformedFrame => GuardCounter.MalformedFrames,
            _ => throw new ArgumentOutOfRangeException(nameof(incident), incident, "only frame incidents are rejected here"),
        };
        _owner!.Cut(this, incident, counter, detail);
    }

    public LoginGate CheckLogin(string? account)
    {
        if (!IsGuarded)
        {
            return LoginGate.Open;
        }

        var gate = _owner!.Logins.Check(Address, account);
        if (gate.LockedOut)
        {
            _owner.Counters.Increment(GuardCounter.LockedOutAttempts);
            _owner.Incident(Record, Address, GuardIncident.LoginLockout, Named(account, "refused without a check"));
        }

        return gate;
    }

    public TimeSpan LoginFailed(string? account)
    {
        if (!IsGuarded)
        {
            return TimeSpan.Zero;
        }

        var failure = _owner!.Logins.Failed(Address, account);
        _owner.Counters.Increment(GuardCounter.LoginFailures);
        if (failure.LockedNow)
        {
            _owner.Counters.Increment(GuardCounter.Lockouts);
            _owner.Incident(Record, Address, GuardIncident.LoginLockout, Named(account, string.Create(CultureInfo.InvariantCulture,
                $"{failure.Failures} failed logins, locked for {_owner.Settings.LockoutMinutes} min")));
        }
        else
        {
            _owner.Incident(Record, Address, GuardIncident.LoginFailure, Named(account, string.Create(CultureInfo.InvariantCulture,
                $"{failure.Failures} in a row, reply delayed {failure.Delay.TotalSeconds:0} s")));
        }

        return failure.Delay;
    }

    public void LoginSucceeded(string? account)
    {
        if (!IsGuarded)
        {
            return;
        }

        _owner!.Logins.Succeeded(account);
        Authenticated();
    }

    public void Authenticated()
    {
        if (!IsGuarded)
        {
            return;
        }

        Interlocked.Exchange(ref _lastFrameAt, _owner!.Now());
        Interlocked.Exchange(ref _authenticated, 1);
        LeavePending();
    }

    public void Buffered(int bytes, bool madeProgress)
    {
        if (!IsGuarded)
        {
            return;
        }

        if (bytes <= 0)
        {
            Interlocked.Exchange(ref _partialSince, 0);
            return;
        }

        if (madeProgress || Interlocked.Read(ref _partialSince) == 0)
        {
            Interlocked.Exchange(ref _partialSince, Math.Max(1, _owner!.Now()));
        }
    }

    private static string Named(string? account, string detail) =>
        string.IsNullOrWhiteSpace(account) ? detail : $"'{account.Trim()}': {detail}";

    private TokenBucket OpcodeBucket(ConnectionGuard owner, ushort opcode, long now)
    {
        _opcodes ??= [];
        if (_opcodes.TryGetValue(opcode, out var bucket))
        {
            return bucket;
        }

        if (_opcodes.Count >= ConnectionGuard.MaxTrackedOpcodes && !owner.HasAllowance(opcode))
        {
            return _otherOpcodes ??= owner.NewOpcodeBucket(opcode, now);
        }

        bucket = owner.NewOpcodeBucket(opcode, now);
        _opcodes[opcode] = bucket;
        return bucket;
    }

    internal bool Abort()
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0)
        {
            return false;
        }

        _abort?.Invoke();
        return true;
    }

    internal bool LeavePending()
    {
        if (Interlocked.Exchange(ref _pending, 0) != 1)
        {
            return false;
        }

        _owner?.PendingLeft();
        return true;
    }

    internal bool MarkReleased() => Interlocked.Exchange(ref _released, 1) == 0;
}
