using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Items;

public static class BossKillLedger
{
    public static IReadOnlyList<BossKillHistoryRecord> Recorded(IReadOnlyList<BossKillHistoryRecord> history, int monsterId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(history);
        var updated = history
            .Select(record => record.MonsterId == monsterId ? record with { KillCount = record.KillCount + 1 } : record)
            .ToList();
        if (!history.Any(record => record.MonsterId == monsterId)) updated.Add(new BossKillHistoryRecord(monsterId, 1, now));
        return updated.OrderBy(record => record.FirstKill).ThenBy(record => record.MonsterId).ToList();
    }
}
