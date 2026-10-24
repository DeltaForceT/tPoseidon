using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using tPoseidon.Core.Common.Security;

namespace tPoseidon.Core.Net.Security;

public readonly record struct Admission(SessionGuard? Session, GuardRefusal Refusal)
{
    public bool Admitted => Session is not null;
}

public sealed class ConnectionGuard : IAsyncDisposable
{
    internal const int MaxTrackedOpcodes = 128;
    internal const int RecentIncidentCount = 50;
    internal const string BanKind = "ban";
    private static readonly TimeSpan ForgetAddressAfter = TimeSpan.FromMinutes(10);

    private readonly GuardSettings _settings;
    private readonly TimeProvider _time;
    private readonly Action<string> _log;
    private readonly AddressRange[] _trusted;
    private readonly bool _trustLoopback;
    private readonly ConcurrentDictionary<IPAddress, AddressRecord> _addresses = new();
    private readonly ConcurrentDictionary<SessionGuard, byte> _sessions = new();
    private readonly IncidentLog _incidents;
    private readonly long _forgetTicks;
    private readonly long _handshakeTicks;
    private readonly long _loginTicks;
    private readonly long _idleTicks;
    private readonly long _partialTicks;
    private readonly Dictionary<ushort, OpcodeAllowance> _allowances;
    private readonly BanRules _banRules;
    private readonly long _banForgetTicks;
    private readonly string? _ledgerPath;
    private readonly object _ledgerGate = new();
    private readonly HashSet<string> _appliedPardons = new(StringComparer.Ordinal);
    private readonly HashSet<string> _kickedBans = new(StringComparer.Ordinal);
    private volatile LedgerView _ledger = LedgerView.Empty;
    private (DateTime Written, long Length) _ledgerStamp;
    private readonly Queue<SnapshotIncident> _recent = new();
    private readonly DateTimeOffset _startedAt;
    private readonly string? _snapshotPath;
    private volatile Dictionary<IPAddress, SnapshotBan> _sharedBans = [];
    private bool _inheritedOwnBans;
    private bool _publishFailed;
    private int _connections;
    private int _pending;
    private int _trustedConnections;
    private CancellationTokenSource? _loopCts;
    private Task? _loop;

    public ConnectionGuard(GuardSettings settings, string serverName, Action<string> log, TimeProvider? time = null, string? stateDirectory = null)
        : this(settings, serverName, log, time ?? TimeProvider.System, trustLoopback: true, stateDirectory)
    {
    }

    internal ConnectionGuard(GuardSettings settings, string serverName, Action<string> log, TimeProvider time, bool trustLoopback, string? stateDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(time);
        _settings = settings;
        ServerName = serverName;
        _log = log;
        _time = time;
        _trustLoopback = trustLoopback;
        _trusted = (settings.TrustedAddresses ?? [])
            .Select(text => AddressRange.TryParse(text, out var range) ? range : null)
            .OfType<AddressRange>()
            .ToArray();
        Logins = new LoginThrottle(settings, time);
        _incidents = new IncidentLog(log, time, TimeSpan.FromSeconds(settings.IncidentLogIntervalSeconds), settings.IncidentLogLinesPerTenSeconds);
        _forgetTicks = IncidentLog.Ticks(ForgetAddressAfter, time);
        _handshakeTicks = IncidentLog.Ticks(TimeSpan.FromSeconds(settings.HandshakeTimeoutSeconds), time);
        _loginTicks = IncidentLog.Ticks(TimeSpan.FromSeconds(settings.LoginTimeoutSeconds), time);
        _idleTicks = IncidentLog.Ticks(TimeSpan.FromSeconds(settings.IdleTimeoutSeconds), time);
        _partialTicks = IncidentLog.Ticks(TimeSpan.FromSeconds(settings.PartialFrameTimeoutSeconds), time);
        StateDirectory = stateDirectory;
        _ledgerPath = stateDirectory is null ? null : SecurityFiles.LedgerPath(stateDirectory);
        _snapshotPath = stateDirectory is null ? null : SecurityFiles.SnapshotPath(stateDirectory, serverName);
        _startedAt = time.GetUtcNow();
        _banForgetTicks = IncidentLog.Ticks(TimeSpan.FromHours(settings.BanForgetHours), time);
        _banRules = new BanRules(
            settings.StrikesBeforeBan,
            IncidentLog.Ticks(TimeSpan.FromSeconds(settings.StrikeWindowSeconds), time),
            settings.BanMinutes,
            settings.BanMaxMinutes,
            _banForgetTicks,
            time.TimestampFrequency);
        _allowances = new Dictionary<ushort, OpcodeAllowance>();
        foreach (var allowance in settings.OpcodeAllowances ?? [])
        {
            if (allowance.Problem() is null && allowance.TryParseOpcode(out var opcode))
            {
                _allowances[opcode] = allowance;
            }
        }
    }

    public string ServerName { get; }

    public string? StateDirectory { get; }

    public GuardSettings Settings => _settings;

    public GuardCounters Counters { get; } = new();

    public LoginThrottle Logins { get; }

    public int Connections => Volatile.Read(ref _connections);

    public int Pending => Volatile.Read(ref _pending);

    public int TrustedConnections => Volatile.Read(ref _trustedConnections);

    public int TrackedAddresses => _addresses.Count;

    public bool ExpectsLogin { get; private set; }

    public bool ClientsKeepAlive { get; private set; }

    internal TimeSpan SweepInterval { get; init; } = TimeSpan.FromSeconds(1);

    internal int SweepsPerPublish { get; init; } = 2;

    public static ConnectionGuard FromEnvironment(string logTag, int port) =>
        FromEnvironment(logTag, string.Create(CultureInfo.InvariantCulture, $"{logTag.Trim('[', ']', ' ')}-{port}"));

    public static ConnectionGuard FromEnvironment(string logTag, string fallbackName)
    {
        ArgumentNullException.ThrowIfNull(logTag);
        ArgumentNullException.ThrowIfNull(fallbackName);
        void Log(string line) => Console.WriteLine($"{logTag} {line}");
        var settings = GuardSettings.FromEnvironment(problem => Log("[guard] " + problem));
        var name = GuardSettings.ServerNameFromEnvironment() ?? fallbackName;
        return new ConnectionGuard(settings, name, Log, TimeProvider.System, GuardSettings.StateDirectoryFromEnvironment());
    }

    public void UseListener(bool expectsLogin, bool clientsKeepAlive)
    {
        ExpectsLogin = expectsLogin;
        ClientsKeepAlive = clientsKeepAlive;
    }

    public string Describe()
    {
        if (!_settings.Enabled)
        {
            return "[guard] off (security.enabled is false)";
        }

        var trusted = _trusted.Length == 0 ? "loopback" : "loopback, " + string.Join(", ", _trusted.Select(range => range.Text));
        return string.Create(CultureInfo.InvariantCulture,
            $"[guard] on: {_settings.MaxConnectionsPerAddress} connections per address, {_settings.NewConnectionsPerAddressBurst} new at once then {_settings.NewConnectionsPerAddressPerSecond:0.##}/s, {_settings.MaxConnections} in all, {_settings.MaxPendingHandshakes} still logging in; first frame within {_settings.HandshakeTimeoutSeconds} s{(ExpectsLogin ? $", login within {_settings.LoginTimeoutSeconds} s" : "")}{(ClientsKeepAlive && _settings.IdleTimeoutSeconds > 0 ? $", idle limit {_settings.IdleTimeoutSeconds} s" : "")}, partial frames within {_settings.PartialFrameTimeoutSeconds} s; frames up to {_settings.MaxFrameBytes} B, {_settings.PacketBurst} frames at once then {_settings.PacketsPerSecond:0.##}/s, {_settings.OpcodeBurst} of one opcode then {_settings.OpcodePacketsPerSecond:0.##}/s ({_allowances.Count} opcodes allowed more); always allowed: {trusted}");
    }

    public bool IsTrusted(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        address = AddressRange.Normalize(address);
        if (_trustLoopback && IPAddress.IsLoopback(address))
        {
            return true;
        }

        foreach (var range in _trusted)
        {
            if (range.Contains(address))
            {
                return true;
            }
        }

        foreach (var range in _ledger.Allow)
        {
            if (range.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsBanned(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        address = AddressRange.Normalize(address);
        if (ManualBan(address) is not null || SharedBan(address) is not null)
        {
            return true;
        }

        if (!_addresses.TryGetValue(address, out var record))
        {
            return false;
        }

        lock (record.Gate)
        {
            return record.IsBanned(_time.GetTimestamp());
        }
    }

    public IReadOnlyList<SnapshotBan> ActiveBans()
    {
        var now = _time.GetTimestamp();
        var nowUtc = _time.GetUtcNow();
        var bans = new List<SnapshotBan>();
        foreach (var (address, record) in _addresses)
        {
            lock (record.Gate)
            {
                if (!record.IsBanned(now))
                {
                    continue;
                }

                bans.Add(new SnapshotBan
                {
                    Address = address.ToString(),
                    Since = record.BannedSince,
                    Until = nowUtc + TimeSpan.FromSeconds((record.BannedUntil - now) / (double)_time.TimestampFrequency),
                    Level = record.BanLevel,
                    Reason = record.BanReason,
                });
            }
        }

        return bans.OrderBy(ban => ban.Until).ToList();
    }

    public void ReloadLedger()
    {
        if (_ledgerPath is null)
        {
            return;
        }

        var stamp = File.Exists(_ledgerPath)
            ? (File.GetLastWriteTimeUtc(_ledgerPath), new FileInfo(_ledgerPath).Length)
            : (DateTime.MinValue, -1L);
        if (stamp == _ledgerStamp)
        {
            return;
        }

        SecurityLedger ledger;
        try
        {
            ledger = SecurityLedger.Load(_ledgerPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _ledgerStamp = stamp;
            _log($"[guard] could not read {SecurityFiles.LedgerFileName} ({ex.Message}); keeping the previous ban and allow lists");
            return;
        }

        _ledgerStamp = stamp;
        ApplyLedger(LedgerView.From(ledger, problem => _log("[guard] " + problem)));
    }

    public Admission Admit(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        address = AddressRange.Normalize(address);
        if (!_settings.Enabled)
        {
            return new Admission(SessionGuard.None, GuardRefusal.None);
        }

        var now = _time.GetTimestamp();
        if (IsTrusted(address))
        {
            Counters.Increment(GuardCounter.TrustedAccepted);
            Interlocked.Increment(ref _trustedConnections);
            return new Admission(new SessionGuard(this, address, null, trusted: true, now), GuardRefusal.None);
        }

        if (ManualBan(address) is not null || SharedBan(address) is not null)
        {
            Counters.Increment(GuardCounter.RefusedBanned);
            return new Admission(null, GuardRefusal.Banned);
        }

        if (Volatile.Read(ref _connections) >= _settings.MaxConnections)
        {
            Counters.Increment(GuardCounter.RefusedServerFull);
            return new Admission(null, GuardRefusal.ServerFull);
        }

        if (Volatile.Read(ref _pending) >= _settings.MaxPendingHandshakes)
        {
            Counters.Increment(GuardCounter.RefusedTooManyPending);
            return new Admission(null, GuardRefusal.TooManyPending);
        }

        AddressRecord record;
        GuardRefusal refusal;
        while (true)
        {
            record = _addresses.GetOrAdd(address, _ => NewRecord(now));
            lock (record.Gate)
            {
                if (record.Retired)
                {
                    continue;
                }

                record.LastSeen = now;
                refusal = record.IsBanned(now)
                    ? GuardRefusal.Banned
                    : record.Connections >= _settings.MaxConnectionsPerAddress
                    ? GuardRefusal.AddressLimit
                    : !record.ConnectBucket.TryTake(now)
                        ? GuardRefusal.AddressRate
                        : GuardRefusal.None;
                if (refusal == GuardRefusal.None)
                {
                    record.Connections++;
                }
            }

            break;
        }

        switch (refusal)
        {
            case GuardRefusal.Banned:
                Counters.Increment(GuardCounter.RefusedBanned);
                return new Admission(null, refusal);
            case GuardRefusal.AddressLimit:
                Counters.Increment(GuardCounter.RefusedAddressLimit);
                Incident(record, address, GuardIncident.ConnectionLimit, string.Create(CultureInfo.InvariantCulture, $"already {_settings.MaxConnectionsPerAddress} open"));
                return new Admission(null, refusal);
            case GuardRefusal.AddressRate:
                Counters.Increment(GuardCounter.RefusedAddressRate);
                Incident(record, address, GuardIncident.ConnectionRate, string.Create(CultureInfo.InvariantCulture, $"more than {_settings.NewConnectionsPerAddressBurst} new connections at once"));
                return new Admission(null, refusal);
        }

        Interlocked.Increment(ref _connections);
        Interlocked.Increment(ref _pending);
        var session = new SessionGuard(this, address, record, trusted: false, now);
        _sessions.TryAdd(session, 0);
        Counters.Increment(GuardCounter.Accepted);
        return new Admission(session, GuardRefusal.None);
    }

    public GuardRefusal AdmitRequest(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        address = AddressRange.Normalize(address);
        if (!_settings.Enabled || IsTrusted(address))
        {
            Counters.Increment(GuardCounter.RequestsAllowed);
            return GuardRefusal.None;
        }

        if (ManualBan(address) is not null || SharedBan(address) is not null)
        {
            Counters.Increment(GuardCounter.RequestsRefused);
            return GuardRefusal.Banned;
        }

        var now = _time.GetTimestamp();
        AddressRecord record;
        GuardRefusal refusal;
        while (true)
        {
            record = _addresses.GetOrAdd(address, _ => NewRecord(now));
            lock (record.Gate)
            {
                if (record.Retired)
                {
                    continue;
                }

                record.LastSeen = now;
                record.RequestBucket ??= new TokenBucket(_settings.RequestBurst, _settings.RequestsPerSecond, now, _time.TimestampFrequency);
                refusal = record.IsBanned(now)
                    ? GuardRefusal.Banned
                    : record.RequestBucket.TryTake(now)
                        ? GuardRefusal.None
                        : GuardRefusal.AddressRate;
            }

            break;
        }

        if (refusal == GuardRefusal.None)
        {
            Counters.Increment(GuardCounter.RequestsAllowed);
            return refusal;
        }

        Counters.Increment(GuardCounter.RequestsRefused);
        if (refusal == GuardRefusal.AddressRate)
        {
            Incident(record, address, GuardIncident.RequestFlood, string.Create(CultureInfo.InvariantCulture,
                $"more than {_settings.RequestBurst} requests at once or {_settings.RequestsPerSecond:0.##}/s"));
        }

        return refusal;
    }

    public void Release(SessionGuard session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.MarkReleased())
        {
            return;
        }

        if (session.IsTrusted)
        {
            if (!ReferenceEquals(session, SessionGuard.None))
            {
                Interlocked.Decrement(ref _trustedConnections);
            }

            return;
        }

        session.LeavePending();
        _sessions.TryRemove(session, out _);
        if (session.Record is { } record)
        {
            lock (record.Gate)
            {
                record.Connections--;
                record.LastSeen = _time.GetTimestamp();
            }
        }

        Interlocked.Decrement(ref _connections);
    }

    public void Start()
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("The guard is already running.");
        }

        _loopCts = new CancellationTokenSource();
        _loop = Task.Run(() => RunAsync(_loopCts.Token));
    }

    public void Sweep()
    {
        ReloadLedger();
        var now = _time.GetTimestamp();
        foreach (var session in _sessions.Keys)
        {
            if (Overdue(session, now) is { } overdue)
            {
                Cut(session, overdue.Incident, overdue.Counter, overdue.Detail);
            }
        }

        foreach (var (address, record) in _addresses)
        {
            lock (record.Gate)
            {
                if (!record.IsIdle(now, _forgetTicks, _banForgetTicks))
                {
                    continue;
                }

                record.Retired = true;
            }

            _addresses.TryRemove(new KeyValuePair<IPAddress, AddressRecord>(address, record));
        }

        Logins.Prune();
        _incidents.Flush();
    }

    public GuardSnapshot Snapshot()
    {
        List<SnapshotIncident> recent;
        lock (_recent)
        {
            recent = [.. _recent];
        }

        return new GuardSnapshot
        {
            Server = ServerName,
            ProcessId = Environment.ProcessId,
            StartedAt = _startedAt,
            UpdatedAt = _time.GetUtcNow(),
            Connections = Connections,
            Pending = Pending,
            TrustedConnections = TrustedConnections,
            TrackedAddresses = TrackedAddresses,
            Counters = Counters.ToDictionary(),
            Bans = [.. ActiveBans()],
            Incidents = recent,
        };
    }

    public void Publish()
    {
        if (StateDirectory is null || _snapshotPath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(StateDirectory);
            ReadSharedBans();
            Snapshot().Write(_snapshotPath);
            _publishFailed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_publishFailed)
            {
                _log($"[guard] could not write the security status to {StateDirectory}: {ex.Message}");
            }

            _publishFailed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_loopCts is null)
        {
            return;
        }

        await _loopCts.CancelAsync().ConfigureAwait(false);
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _loopCts.Dispose();
        _loopCts = null;
        _loop = null;
        Publish();
    }

    internal void PendingLeft() => Interlocked.Decrement(ref _pending);

    internal long Now() => _time.GetTimestamp();

    internal bool HasAllowance(ushort opcode) => _allowances.ContainsKey(opcode);

    internal TokenBucket NewPacketBucket(long now) =>
        new(_settings.PacketBurst, _settings.PacketsPerSecond, now, _time.TimestampFrequency);

    internal TokenBucket NewOpcodeBucket(ushort opcode, long now) =>
        _allowances.TryGetValue(opcode, out var allowance)
            ? new TokenBucket(allowance.Burst, allowance.PerSecond, now, _time.TimestampFrequency)
            : new TokenBucket(_settings.OpcodeBurst, _settings.OpcodePacketsPerSecond, now, _time.TimestampFrequency);

    internal void Cut(SessionGuard session, GuardIncident incident, GuardCounter counter, string detail)
    {
        if (!session.Abort())
        {
            return;
        }

        Counters.Increment(counter);
        Incident(session.Record, session.Address, incident, detail);
    }

    internal void Incident(AddressRecord? record, IPAddress address, GuardIncident incident, string detail)
    {
        _incidents.Report(incident, address, detail);
        Remember(address, GuardIncidents.Key(incident), detail);
        var weight = GuardIncidents.Strikes(incident);
        if (record is null || weight == 0)
        {
            return;
        }

        (TimeSpan Length, int Level, int Strikes)? ban;
        lock (record.Gate)
        {
            ban = record.AddStrike(_time.GetTimestamp(), weight, _banRules, _time.GetUtcNow(), GuardIncidents.Describe(incident));
        }

        if (ban is not { } banned)
        {
            return;
        }

        Counters.Increment(GuardCounter.Bans);
        var summary = string.Create(CultureInfo.InvariantCulture,
            $"for {Length(banned.Length)} (ban {banned.Level}): {banned.Strikes} strikes within {_settings.StrikeWindowSeconds} s, last: {GuardIncidents.Describe(incident)}");
        _incidents.Notice($"banned {address} {summary}");
        Remember(address, BanKind, summary);
        Kick(range: null, address, includeLoggedIn: false);
    }

    private void Remember(IPAddress address, string kind, string detail)
    {
        var entry = new SnapshotIncident { At = _time.GetUtcNow(), Address = address.ToString(), Kind = kind, Detail = detail };
        lock (_recent)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > RecentIncidentCount)
            {
                _recent.Dequeue();
            }
        }
    }

    private SnapshotBan? SharedBan(IPAddress address) =>
        _sharedBans.TryGetValue(address, out var ban) && ban.Until > _time.GetUtcNow() ? ban : null;

    private void ReadSharedBans()
    {
        var nowUtc = _time.GetUtcNow();
        var pardons = _ledger.Pardons;
        var shared = new Dictionary<IPAddress, SnapshotBan>();
        foreach (var (path, snapshot) in SecurityFiles.ReadSnapshots(StateDirectory!))
        {
            var own = string.Equals(Path.GetFullPath(path), Path.GetFullPath(_snapshotPath!), StringComparison.OrdinalIgnoreCase);
            if (own && _inheritedOwnBans)
            {
                continue;
            }

            foreach (var ban in snapshot.Bans)
            {
                if (ban.Until <= nowUtc || !IPAddress.TryParse(ban.Address, out var address))
                {
                    continue;
                }

                address = AddressRange.Normalize(address);
                if (pardons.Any(pardon => pardon.Range.Contains(address) && pardon.At >= ban.Since))
                {
                    continue;
                }

                if (!shared.TryGetValue(address, out var known) || known.Until < ban.Until)
                {
                    shared[address] = new SnapshotBan { Address = ban.Address, Since = ban.Since, Until = ban.Until, Level = ban.Level, Reason = $"{ban.Reason} ({snapshot.Server})" };
                }
            }
        }

        _inheritedOwnBans = true;
        _sharedBans = shared;
    }

    private ManualBanView? ManualBan(IPAddress address)
    {
        var nowUtc = _time.GetUtcNow();
        foreach (var ban in _ledger.Bans)
        {
            if ((ban.Until is null || ban.Until > nowUtc) && ban.Range.Contains(address))
            {
                return ban;
            }
        }

        return null;
    }

    private void ApplyLedger(LedgerView view)
    {
        _ledger = view;
        var kicks = new List<ManualBanView>();
        var pardons = new List<LedgerPardonView>();
        lock (_ledgerGate)
        {
            foreach (var ban in view.Bans)
            {
                if (_kickedBans.Add(ban.Key))
                {
                    kicks.Add(ban);
                }
            }

            foreach (var pardon in view.Pardons)
            {
                if (_appliedPardons.Add(pardon.Key))
                {
                    pardons.Add(pardon);
                }
            }
        }

        foreach (var ban in kicks)
        {
            var kicked = Kick(ban.Range, address: null, includeLoggedIn: true);
            var until = ban.Until is { } end ? string.Create(CultureInfo.InvariantCulture, $" until {end:u}") : "";
            var closed = kicked > 0 ? string.Create(CultureInfo.InvariantCulture, $", closed {kicked} sessions") : "";
            _incidents.Notice($"manual ban on {ban.Range}{until}{closed}");
        }

        foreach (var pardon in pardons)
        {
            var lifted = 0;
            foreach (var (address, record) in _addresses)
            {
                if (!pardon.Range.Contains(address))
                {
                    continue;
                }

                lock (record.Gate)
                {
                    lifted += record.Pardon(pardon.At) ? 1 : 0;
                }
            }

            Logins.Pardon(pardon.Range);
            if (lifted > 0)
            {
                _incidents.Notice($"unbanned {pardon.Range}");
            }
        }
    }

    private int Kick(AddressRange? range, IPAddress? address, bool includeLoggedIn)
    {
        var kicked = 0;
        foreach (var session in _sessions.Keys)
        {
            var matches = range?.Contains(session.Address) ?? session.Address.Equals(address);
            if (!matches || (!includeLoggedIn && session.IsAuthenticated))
            {
                continue;
            }

            if (session.Abort())
            {
                kicked++;
                Counters.Increment(GuardCounter.BanKicks);
            }
        }

        return kicked;
    }

    private static string Length(TimeSpan length) =>
        length.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{length.TotalHours:0.#} h")
            : string.Create(CultureInfo.InvariantCulture, $"{length.TotalMinutes:0} min");

    private (GuardIncident Incident, GuardCounter Counter, string Detail)? Overdue(SessionGuard session, long now)
    {
        var partialSince = session.PartialSince;
        if (partialSince != 0 && now - partialSince > _partialTicks)
        {
            return (GuardIncident.PartialFrame, GuardCounter.PartialFrames, Seconds("a frame stayed incomplete for", _settings.PartialFrameTimeoutSeconds));
        }

        if (!session.HasSentFrame && now - session.AcceptedAt > _handshakeTicks)
        {
            return (GuardIncident.HandshakeTimeout, GuardCounter.HandshakeTimeouts, Seconds("nothing valid received in", _settings.HandshakeTimeoutSeconds));
        }

        if (ExpectsLogin && !session.IsAuthenticated && now - session.AcceptedAt > _loginTicks)
        {
            return (GuardIncident.LoginTimeout, GuardCounter.LoginTimeouts, Seconds("not logged in after", _settings.LoginTimeoutSeconds));
        }

        if (ClientsKeepAlive && _idleTicks > 0 && session.IsAuthenticated && now - session.LastFrameAt > _idleTicks)
        {
            return (GuardIncident.IdleTimeout, GuardCounter.IdleTimeouts, Seconds("silent for", _settings.IdleTimeoutSeconds));
        }

        return null;
    }

    private static string Seconds(string what, int seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{what} {seconds} s, closed");

    private AddressRecord NewRecord(long now) => new(
        new TokenBucket(_settings.NewConnectionsPerAddressBurst, _settings.NewConnectionsPerAddressPerSecond, now, _time.TimestampFrequency),
        now);

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(SweepInterval, _time);
        var ticks = 0;
        Publish();
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                Sweep();
                if (++ticks % SweepsPerPublish == 0)
                {
                    Publish();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log($"[guard] sweep failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}

internal sealed record ManualBanView(AddressRange Range, DateTimeOffset? Until, string Reason, string Key);

internal sealed record LedgerPardonView(AddressRange Range, DateTimeOffset At, string Key);

internal sealed record LedgerView(IReadOnlyList<ManualBanView> Bans, IReadOnlyList<AddressRange> Allow, IReadOnlyList<LedgerPardonView> Pardons)
{
    public static readonly LedgerView Empty = new([], [], []);

    public static LedgerView From(SecurityLedger ledger, Action<string> problem)
    {
        var bans = new List<ManualBanView>();
        foreach (var ban in ledger.Bans)
        {
            if (AddressRange.TryParse(ban.Address, out var range))
            {
                bans.Add(new ManualBanView(range, ban.Until, ban.Reason, string.Create(CultureInfo.InvariantCulture, $"{range}|{ban.Since:O}")));
            }
            else
            {
                problem($"ignoring ban entry '{ban.Address}': not an address or range");
            }
        }

        var allow = new List<AddressRange>();
        foreach (var entry in ledger.Allow)
        {
            if (AddressRange.TryParse(entry.Address, out var range))
            {
                allow.Add(range);
            }
            else
            {
                problem($"ignoring allow entry '{entry.Address}': not an address or range");
            }
        }

        var pardons = new List<LedgerPardonView>();
        foreach (var pardon in ledger.Pardons)
        {
            if (AddressRange.TryParse(pardon.Address, out var range))
            {
                pardons.Add(new LedgerPardonView(range, pardon.At, string.Create(CultureInfo.InvariantCulture, $"{range}|{pardon.At:O}")));
            }
        }

        return new LedgerView(bans, allow, pardons);
    }
}
