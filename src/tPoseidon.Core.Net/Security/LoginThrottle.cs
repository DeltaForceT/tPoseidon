using System.Collections.Concurrent;
using System.Net;
using tPoseidon.Core.Common.Security;

namespace tPoseidon.Core.Net.Security;

public readonly record struct LoginGate(bool LockedOut, TimeSpan Delay)
{
    public static readonly LoginGate Open = new(false, TimeSpan.Zero);
}

public readonly record struct LoginFailure(TimeSpan Delay, int Failures, bool LockedNow);

public sealed class LoginThrottle
{
    internal const int MaxTrackedAccounts = 100_000;

    private readonly GuardSettings _settings;
    private readonly TimeProvider _time;
    private readonly long _windowTicks;
    private readonly long _lockoutTicks;
    private readonly ConcurrentDictionary<IPAddress, FailureRecord> _addresses = new();
    private readonly ConcurrentDictionary<string, FailureRecord> _accounts = new(StringComparer.Ordinal);

    public LoginThrottle(GuardSettings settings, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(time);
        _settings = settings;
        _time = time;
        _windowTicks = IncidentLog.Ticks(TimeSpan.FromMinutes(settings.LoginFailureWindowMinutes), time);
        _lockoutTicks = IncidentLog.Ticks(TimeSpan.FromMinutes(settings.LockoutMinutes), time);
    }

    public int TrackedAddresses => _addresses.Count;

    public int TrackedAccounts => _accounts.Count;

    private TimeSpan LockedDelay => TimeSpan.FromSeconds(_settings.LoginDelayMaxSeconds);

    public LoginGate Check(IPAddress address, string? account)
    {
        ArgumentNullException.ThrowIfNull(address);
        var now = _time.GetTimestamp();
        var locked = IsLocked(_addresses.GetValueOrDefault(address), now)
            || (Key(account) is { } key && IsLocked(_accounts.GetValueOrDefault(key), now));
        return locked ? new LoginGate(true, LockedDelay) : LoginGate.Open;
    }

    public LoginFailure Failed(IPAddress address, string? account)
    {
        ArgumentNullException.ThrowIfNull(address);
        var now = _time.GetTimestamp();
        var (addressFailures, addressLocked) = Count(_addresses.GetOrAdd(address, _ => new FailureRecord()), now, _settings.AddressLockoutFailures);
        var accountFailures = 0;
        var accountLocked = false;
        if (Key(account) is { } key && (_accounts.Count < MaxTrackedAccounts || _accounts.ContainsKey(key)))
        {
            (accountFailures, accountLocked) = Count(_accounts.GetOrAdd(key, _ => new FailureRecord()), now, _settings.AccountLockoutFailures);
        }

        var failures = Math.Max(addressFailures, accountFailures);
        var lockedNow = addressLocked || accountLocked;
        return new LoginFailure(lockedNow ? LockedDelay : DelayFor(failures), failures, lockedNow);
    }

    public void Succeeded(string? account)
    {
        if (Key(account) is not { } key || !_accounts.TryGetValue(key, out var record))
        {
            return;
        }

        lock (record)
        {
            if (record.LockedUntil > _time.GetTimestamp())
            {
                return;
            }

            record.Failures = 0;
        }
    }

    public int Pardon(AddressRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        var removed = 0;
        foreach (var address in _addresses.Keys)
        {
            if (range.Contains(address) && _addresses.TryRemove(address, out _))
            {
                removed++;
            }
        }

        return removed;
    }

    public void Prune()
    {
        var now = _time.GetTimestamp();
        Prune(_addresses, now);
        Prune(_accounts, now);
    }

    internal TimeSpan DelayFor(int failures)
    {
        var over = failures - _settings.LoginFailuresBeforeDelay;
        if (over <= 0)
        {
            return TimeSpan.Zero;
        }

        var seconds = over >= 31 ? _settings.LoginDelayMaxSeconds : Math.Min(_settings.LoginDelayMaxSeconds, 1L << (over - 1));
        return TimeSpan.FromSeconds(seconds);
    }

    private (int Failures, bool LockedNow) Count(FailureRecord record, long now, int lockoutAt)
    {
        lock (record)
        {
            if (record.Failures == 0 || now - record.WindowStart > _windowTicks)
            {
                record.Failures = 0;
                record.WindowStart = now;
            }

            record.Failures++;
            record.LastFailure = now;
            var failures = record.Failures;
            if (failures < lockoutAt)
            {
                return (failures, false);
            }

            record.LockedUntil = now + _lockoutTicks;
            record.Failures = 0;
            return (failures, true);
        }
    }

    private void Prune<TKey>(ConcurrentDictionary<TKey, FailureRecord> records, long now)
        where TKey : notnull
    {
        foreach (var (key, record) in records)
        {
            bool stale;
            lock (record)
            {
                stale = record.LockedUntil <= now && now - record.LastFailure > _windowTicks;
            }

            if (stale)
            {
                records.TryRemove(new KeyValuePair<TKey, FailureRecord>(key, record));
            }
        }
    }

    private static bool IsLocked(FailureRecord? record, long now)
    {
        if (record is null)
        {
            return false;
        }

        lock (record)
        {
            return record.LockedUntil > now;
        }
    }

    private static string? Key(string? account)
    {
        var trimmed = account?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToUpperInvariant();
    }

    private sealed class FailureRecord
    {
        public int Failures { get; set; }

        public long WindowStart { get; set; }

        public long LastFailure { get; set; }

        public long LockedUntil { get; set; }
    }
}
