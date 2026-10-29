namespace tPoseidon.Core.Data;

public readonly record struct UniqueHeldItem(ulong ItemUid, int ItemIndex);

public sealed record UniqueItemInfo(
    int ItemIndex,
    byte UniqueType,
    int LimitCount,
    int TotalCount,
    int ServerCount,
    int UserCount,
    int BaseCount,
    int CurrentCount,
    char UpdateFlag,
    int DayLimitCount);

public sealed record UniquePickup(byte UniqueType, DateTimeOffset? DutyEnd);

public sealed record UniqueDutyOutcome(
    IReadOnlyList<(ulong CharUid, DateTimeOffset DutyEnd)> Accomplished,
    IReadOnlyList<ulong> Failed);

public static class UniqueItemRules
{
    public const byte AbsoluteUnique = 1;
    public const byte NormalUnique = 2;
    public const byte EventUnique = 3;
    public const int NotHeld = 106;
    public const int MaxWithdrawnPerRequest = 3;
    public const int PickupLog = 1;
    public const int DutyAccomplishedLog = 0;
    public const int DutyDeprivedLog = 1;
    public const int DutyScheduleLog = 0;
    public static readonly TimeSpan DutyPeriod = TimeSpan.FromDays(7);
    public static readonly TimeSpan DutyCheckInterval = TimeSpan.FromMilliseconds(3_600_000);
    public const char Transferred = 'T';
    public const char Added = 'A';
    public const char Modified = 'M';
    public const char Removed = 'R';
    public static readonly int[] NoSealMonsters = [2214, 2215];
    public const int NotReached = 105;
    public const int IssueLog = 0;
    public const short ActiveTarget = 1;
    public const short UsedTarget = 2;

    public static int RandomTarget(int baseCount, Func<int, int> rollBelow)
    {
        ArgumentNullException.ThrowIfNull(rollBelow);
        return rollBelow(Math.Max(0, baseCount) + 1);
    }

    public static bool EarnsSeal(int monsterId) => !NoSealMonsters.Contains(monsterId);

    public static bool SealCredited(int monsterId, DateTimeOffset? dutyEnd, DateTimeOffset now) =>
        EarnsSeal(monsterId) && dutyEnd is { } end && end >= now;

    public static bool CarriesDuty(byte uniqueType) => uniqueType == AbsoluteUnique;

    public static bool DutyAccomplished(int bossMedals, int uniqueItems) => bossMedals >= uniqueItems;

    public static bool DutyCheckDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now - last > DutyCheckInterval;
}

public readonly record struct BossKillRecord(int MonsterId, DateTimeOffset LatestKill);

public readonly record struct BossKillHistoryRecord(int MonsterId, int KillCount, DateTimeOffset FirstKill);

public interface IUniqueItemStore
{
    IReadOnlyList<UniqueItemInfo> MasterRestart();

    UniquePickup? PickedUp(ulong charUid, ulong itemUid, int itemIndex, DateTimeOffset now);

    UniqueDutyOutcome ApplyDuty(bool deprive, DateTimeOffset now);

    IReadOnlyList<UniqueHeldItem> DepriveAll(ulong charUid);

    IReadOnlyList<UniqueHeldItem> DepriveSingle(ulong charUid, IReadOnlyList<UniqueHeldItem> items);

    bool BossKilled(ulong charUid, int monsterId, DateTimeOffset now);

    IReadOnlyList<BossKillRecord> LatestBossKills(ulong charUid);

    IReadOnlyList<BossKillHistoryRecord> BossKillHistory(ulong charUid);

    bool AddCount(int itemIndex, int units, DateTimeOffset now);
}
