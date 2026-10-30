using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public interface IMegaphoneDelivery
{
    IReadOnlyCollection<ushort> Targets();

    Task SendAsync(ushort target, QueuedMegaphone shout, CancellationToken ct);

    Task CheckValidityAsync(ushort target, CancellationToken ct);
}

public readonly record struct MegaphoneEnqueued(bool Accepted, uint Value);

public sealed class MegaphonePacer
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private readonly IMegaphoneQueueStore _store;
    private readonly IMegaphoneDelivery _delivery;
    private readonly TimeProvider _time;
    private readonly TimeSpan _delay;
    private readonly Action<string> _log;
    private readonly object _gate = new();
    private long? _lastPoll;
    private long? _lastPost;
    private bool _faulted;
    private Awaiting? _awaiting;

    public MegaphonePacer(IMegaphoneQueueStore store, IMegaphoneDelivery delivery, TimeProvider time, int delayMilliseconds, Action<string> log)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delayMilliseconds);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _delay = TimeSpan.FromMilliseconds(delayMilliseconds);
    }

    public bool Enabled => _delay > TimeSpan.Zero;

    public MegaphoneEnqueued Enqueue(ulong characterId, string message, MegaphoneRefund refund)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (characterId == 0)
        {
            _log($"megaphone: invalid character 0x{characterId:X}");
            return new MegaphoneEnqueued(false, MegaphoneErrors.InvalidCharacter);
        }
        lock (_gate)
        {
            if (_faulted)
            {
                _log($"megaphone: fault state, 0x{characterId:X} refused");
                return new MegaphoneEnqueued(false, MegaphoneErrors.Fault);
            }
            int waiting;
            try
            {
                waiting = _store.Enqueue(characterId, message, refund);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log($"megaphone: store failed for 0x{characterId:X}: {ex.GetType().Name}: {ex.Message}");
                return new MegaphoneEnqueued(false, MegaphoneErrors.StoreFailure);
            }
            _log($"megaphone: 0x{characterId:X} queued behind {waiting}: {message}");
            if (Idle(_time.GetTimestamp()))
            {
                _lastPoll = null;
            }
            return new MegaphoneEnqueued(true, (uint)waiting);
        }
    }

    public async Task TickAsync(CancellationToken ct)
    {
        if (!Enabled) return;
        var now = _time.GetTimestamp();
        QueuedMegaphone? toPost;
        IReadOnlyCollection<ushort> targets;
        IReadOnlyCollection<ushort> checks;
        lock (_gate)
        {
            if (!Elapsed(_lastPoll, now)) return;
            _lastPoll = now;
            toPost = Poll(now, out targets, out checks);
        }
        foreach (var target in checks)
        {
            try
            {
                await _delivery.CheckValidityAsync(target, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"megaphone: validity check to {target} failed: {ex.Message}");
            }
        }
        if (toPost is null) return;
        foreach (var target in targets)
        {
            try
            {
                await _delivery.SendAsync(target, toPost, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"megaphone: sending {toPost.Code} to {target} failed: {ex.Message}");
                lock (_gate)
                {
                    Withdraw(toPost.Code, target);
                }
            }
        }
    }

    public void Acknowledge(ushort source, long code)
    {
        lock (_gate)
        {
            if (_awaiting is null)
            {
                _log($"megaphone: acknowledgement {code} from {source} with nothing in flight");
                return;
            }
            if (_awaiting.Code != code)
            {
                _log($"megaphone: invalid acknowledgement from {source}: expected {_awaiting.Code}, received {code}");
                return;
            }
            _awaiting.Pending.Remove(source);
            if (_awaiting.Pending.Count == 0)
            {
                Complete(code);
            }
        }
    }

    public void ValidityAnswered(ushort source)
    {
        lock (_gate)
        {
            if (_awaiting?.Validating is not { } validating || !validating.Remove(source))
            {
                _log($"megaphone: validity answer from {source} with no check waiting for it");
                return;
            }
            if (validating.Count == 0)
            {
                _log($"megaphone: {_awaiting.Code} validity confirmed by every system server, posting it again");
                _awaiting = null;
            }
        }
    }

    private QueuedMegaphone? Poll(long now, out IReadOnlyCollection<ushort> targets, out IReadOnlyCollection<ushort> checks)
    {
        targets = [];
        checks = [];
        QueuedMegaphone? next;
        try
        {
            next = _store.NextPending();
            _faulted = false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _faulted = true;
            _log($"megaphone: reading the queue failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
        if (next is null)
        {
            _awaiting = null;
            return null;
        }
        if (_awaiting is not null)
        {
            _awaiting.Pending.IntersectWith(_delivery.Targets());
            if (_awaiting.Pending.Count == 0)
            {
                Complete(_awaiting.Code);
                return null;
            }
            checks = _delivery.Targets();
            _awaiting.Validating = checks.ToHashSet();
            _log($"megaphone: {_awaiting.Code} not acknowledged, checking validity of {checks.Count} system server(s)");
            return null;
        }
        targets = _delivery.Targets();
        _lastPost = now;
        if (targets.Count == 0)
        {
            _log($"megaphone: {next.Code} not posted, no system server reachable");
            return null;
        }
        _awaiting = new Awaiting(next.Code, targets.ToHashSet());
        _log($"megaphone: {next.Code} posted to {targets.Count} system server(s)");
        return next;
    }

    private void Withdraw(long code, ushort target)
    {
        if (_awaiting is null || _awaiting.Code != code) return;
        _awaiting.Pending.Remove(target);
        if (_awaiting.Pending.Count == 0)
        {
            _awaiting = null;
            _log($"megaphone: {code} reached no system server, it stays queued");
        }
    }

    private void Complete(long code)
    {
        _awaiting = null;
        try
        {
            _log(_store.MarkDelivered(code) ? $"megaphone: {code} delivered" : $"megaphone: {code} was not pending");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _faulted = true;
            _log($"megaphone: marking {code} delivered failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool Idle(long now) => Elapsed(_lastPoll, now) && Elapsed(_lastPost, now);

    private bool Elapsed(long? since, long now) => since is not { } start || _time.GetElapsedTime(start, now) >= _delay;

    private sealed record Awaiting(long Code, HashSet<ushort> Pending)
    {
        public HashSet<ushort>? Validating { get; set; }
    }
}
