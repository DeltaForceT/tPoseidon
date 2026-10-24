namespace tPoseidon.Core.Net.S2;

public enum HeartbeatStep
{
    Wait,
    Ping,
    Expired,
}

public sealed class ClientHeartbeat
{
    public static readonly TimeSpan FirstPingDelay = TimeSpan.FromMilliseconds(5000);
    public static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(10000 * 6);
    public static readonly TimeSpan DefaultAnswerTimeout = TimeSpan.FromMilliseconds(33333);
    public static readonly TimeSpan CheckPeriod = TimeSpan.FromSeconds(1);

    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private DateTimeOffset _lastPingAt;
    private TimeSpan _interval = FirstPingDelay;
    private bool _awaitingAnswer;
    private bool _started;

    public ClientHeartbeat(TimeSpan answerTimeout, TimeProvider? time = null)
    {
        if (answerTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(answerTimeout), answerTimeout, "The answer timeout must be positive.");
        }

        AnswerTimeout = answerTimeout;
        _time = time ?? TimeProvider.System;
    }

    public TimeSpan AnswerTimeout { get; }

    public static TimeSpan AnswerTimeoutFromIni(int heartbeatMilliseconds) =>
        heartbeatMilliseconds > 0 ? TimeSpan.FromMilliseconds(heartbeatMilliseconds) : DefaultAnswerTimeout;

    public void Start()
    {
        lock (_lock)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _lastPingAt = _time.GetUtcNow();
        }
    }

    public HeartbeatStep Tick()
    {
        lock (_lock)
        {
            if (!_started)
            {
                return HeartbeatStep.Wait;
            }

            var now = _time.GetUtcNow();
            var elapsed = now - _lastPingAt;
            if (_awaitingAnswer)
            {
                return elapsed > AnswerTimeout ? HeartbeatStep.Expired : HeartbeatStep.Wait;
            }

            if (elapsed <= _interval)
            {
                return HeartbeatStep.Wait;
            }

            _awaitingAnswer = true;
            _lastPingAt = now;
            return HeartbeatStep.Ping;
        }
    }

    public void Answered()
    {
        lock (_lock)
        {
            _awaitingAnswer = false;
            _interval = PingInterval;
        }
    }

    public async Task RunAsync(Func<CancellationToken, Task> ping, Func<Task> expire, Func<bool> connected, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ping);
        ArgumentNullException.ThrowIfNull(expire);
        ArgumentNullException.ThrowIfNull(connected);
        using var timer = new PeriodicTimer(CheckPeriod, _time);
        while (connected() && await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!connected())
            {
                return;
            }

            switch (Tick())
            {
                case HeartbeatStep.Ping:
                    await ping(cancellationToken).ConfigureAwait(false);
                    break;
                case HeartbeatStep.Expired:
                    await expire().ConfigureAwait(false);
                    return;
            }
        }
    }
}
