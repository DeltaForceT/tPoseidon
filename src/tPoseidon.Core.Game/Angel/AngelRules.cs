namespace tPoseidon.Core.Game.Angel;

public enum AngelRequestState : byte
{
    None = 0,
    Requested = 1,
    Accepted = 2,
    Ended = 3,
}

public static class AngelRules
{
    public const int NewbieLevelLimit = 0x1F;
    public const int AngelLevelFloor = 0x32;
    public const int ListCapacity = 10;
    public const int ShortReportLimit = 4;

    public const byte AllBusy = 1;
    public const byte Unreachable = 2;
    public const byte SameAngelTooSoon = 3;
    public const byte HelpingAnother = 4;
    public const byte RemovedByAngel = 5;
    public const byte CannotRespond = 7;
    public const byte RudeNewbie = 8;
    public const byte AcceptFailed = 1;

    public const byte RequestNextFree = 0;
    public const byte RequestListed = 1;

    public static readonly TimeSpan RequestHold = TimeSpan.FromSeconds(300);
    public static readonly TimeSpan EndedHold = TimeSpan.FromSeconds(0x708);
    public static readonly TimeSpan ShortRudeWait = TimeSpan.FromSeconds(300);
    public static readonly TimeSpan LongRudeWait = TimeSpan.FromSeconds(0x708);

    public static readonly TimeSpan WingTime = TimeSpan.FromSeconds(604800);

    public static bool IsNewbie(int level) => level < NewbieLevelLimit;

    public static bool Graduates(int level) => level >= NewbieLevelLimit;

    public static bool WingActive(DateTimeOffset latestGraduation, DateTimeOffset now) => now - latestGraduation < WingTime;

    public static bool IsAngel(int level) => level > AngelLevelFloor;

    public static bool RecentlyRequested(AngelRequestState state, DateTimeOffset at, DateTimeOffset now) =>
        state == AngelRequestState.Requested && now - at < RequestHold;

    public static bool Engaged(AngelRequestState state, DateTimeOffset at, DateTimeOffset now) =>
        state == AngelRequestState.Accepted || (state == AngelRequestState.Ended && now - at < EndedHold);

    public static bool IsFree(AngelRequestState state, DateTimeOffset at, DateTimeOffset now) =>
        !RecentlyRequested(state, at, now) && !Engaged(state, at, now);

    public static byte? RefusalFor(AngelRequestState state, DateTimeOffset at, DateTimeOffset now)
    {
        if (RecentlyRequested(state, at, now)) return SameAngelTooSoon;
        if (Engaged(state, at, now)) return HelpingAnother;
        return null;
    }

    public static TimeSpan RudeWait(int reports) => reports < ShortReportLimit ? ShortRudeWait : LongRudeWait;

    public static TimeSpan? RemainingRudeWait(int reports, DateTimeOffset reportedAt, DateTimeOffset now)
    {
        if (reports <= 0) return null;
        var left = reportedAt + RudeWait(reports) - now;
        return left > TimeSpan.Zero ? left : null;
    }
}
