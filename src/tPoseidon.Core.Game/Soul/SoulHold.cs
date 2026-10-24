using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Soul;

public sealed class SoulHold
{
    private readonly Dictionary<ulong, GuardianSoulRow> _guardians = new();
    private readonly Dictionary<ulong, DateTimeOffset> _drainDue = new();

    public SoulHold(uint soul, IEnumerable<GuardianSoulRow> guardians)
    {
        ArgumentNullException.ThrowIfNull(guardians);
        Soul = soul;
        foreach (var row in guardians) _guardians[row.GuardianUid] = row;
    }

    public object Gate { get; } = new();

    public uint Soul { get; set; }

    public bool Busy { get; set; }

    public DateTimeOffset? NextDrainCheck { get; set; }

    public GuardianSoulRow? Guardian(ulong guardianUid) => _guardians.TryGetValue(guardianUid, out var row) ? row : null;

    public void Set(GuardianSoulRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        _guardians[row.GuardianUid] = row;
    }

    public DateTimeOffset? DrainDue(ulong itemUid) => _drainDue.TryGetValue(itemUid, out var due) ? due : null;

    public void SetDrainDue(ulong itemUid, DateTimeOffset due) => _drainDue[itemUid] = due;

    public void ForgetDrains(IEnumerable<ulong> keep)
    {
        ArgumentNullException.ThrowIfNull(keep);
        var kept = keep.ToHashSet();
        foreach (var uid in _drainDue.Keys.Where(uid => !kept.Contains(uid)).ToList()) _drainDue.Remove(uid);
    }
}
