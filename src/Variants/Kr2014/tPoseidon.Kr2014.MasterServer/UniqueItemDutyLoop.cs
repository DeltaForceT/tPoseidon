using tPoseidon.Core.Data;

namespace tPoseidon.Kr2014.MasterServer;

public sealed class UniqueItemDutyLoop
{
    private static readonly TimeSpan Poll = TimeSpan.FromMinutes(1);
    private readonly IUniqueItemStore _store;
    private readonly Action<string> _log;
    private readonly Func<ulong, DateTimeOffset, DateTimeOffset, Task> _sendExpire;
    private DateTimeOffset? _lastCheck;

    public UniqueItemDutyLoop(IUniqueItemStore store, Func<ulong, DateTimeOffset, DateTimeOffset, Task> sendExpire, Action<string> log)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _sendExpire = sendExpire ?? throw new ArgumentNullException(nameof(sendExpire));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task StartAsync()
    {
        var rows = _store.MasterRestart();
        _log($"unique items: {rows.Count} rows after master restart ({rows.Count(row => row.UniqueType == UniqueItemRules.AbsoluteUnique)} absolute)");
        await SendExpiresAsync(Check(deprive: true)).ConfigureAwait(false);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Poll);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            if (!UniqueItemRules.DutyCheckDue(_lastCheck, DateTimeOffset.UtcNow)) continue;
            try
            {
                var outcome = Check(deprive: false);
                await SendExpiresAsync(outcome).ConfigureAwait(false);
                foreach (var charUid in outcome.Failed)
                {
                    var taken = _store.DepriveAll(charUid);
                    _log($"unique duty failed for 0x{charUid:X16}: {taken.Count} absolute unique items withdrawn");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log($"unique duty check threw {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private async Task SendExpiresAsync(UniqueDutyOutcome outcome)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (charUid, end) in outcome.Accomplished)
        {
            await _sendExpire(charUid, end, now).ConfigureAwait(false);
        }
    }

    private UniqueDutyOutcome Check(bool deprive)
    {
        var now = DateTimeOffset.UtcNow;
        var outcome = _store.ApplyDuty(deprive, now);
        _lastCheck = now;
        _log($"unique duty check (deprive {deprive}): {outcome.Accomplished.Count} accomplished, {outcome.Failed.Count} failed");
        return outcome;
    }
}
