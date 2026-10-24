using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Wedding;

public readonly record struct ClosenessResult(byte Relation, byte Love, byte Friend, long EventMask, byte State);

public static class CoupleCloseness
{
    public const int CouplePartySmall = 0x01;
    public const int PartyWithoutLover = 0x02;
    public const int CouplePartyLarge = 0x04;
    public const int MemoToLover = 0x08;
    public const int MemoToOthers = 0x10;
    public const int BothOnline = 0x20;
    public const int WhisperToLover = 0x40;
    public const int CouplePartyQuest = 0x80;
    public const int LoverPosition = 0x100;
    public const int Anniversary = 0x200;
    public const int GiftFromLover = 0x400;
    public const int DailyReset = 0;
    public const int SmallPartyLimit = 3;
    public const int MinPoint = 0;
    public const int MaxPoint = 100;
    public const int CloseEnoughRelation = 86;
    public const int FirstAnniversaryDays = 50;
    public const int AnniversaryPeriodDays = 100;
    public const byte LastDatingState = 1;
    public const byte FirstStateAfterGm = 9;

    public static bool Counts(byte state) => state <= LastDatingState || state >= FirstStateAfterGm;

    public static int PartyEvent(bool loverInParty, int partySize) =>
        !loverInParty ? PartyWithoutLover : partySize < SmallPartyLimit ? CouplePartySmall : CouplePartyLarge;

    public static bool IsAnniversary(DateTimeOffset coupleStart, DateTimeOffset now)
    {
        var days = (int)(now.Date - coupleStart.Date).TotalDays;
        return days > 0 && (days == FirstAnniversaryDays || days % AnniversaryPeriodDays == 0);
    }

    public static long MaskToday(long mask, DateTimeOffset lastEvent, DateTimeOffset now) => lastEvent.Date == now.Date ? mask : 0;

    public static ClosenessResult? Apply(
        byte state, byte relation, byte love, byte friend, long maskToday, int eventBit, IReadOnlyDictionary<int, CoupleEventPoints> table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!Counts(state)) return null;
        if (eventBit == DailyReset) return new ClosenessResult(relation, love, friend, 0, state);
        var daily = eventBit != GiftFromLover;
        if (daily && (maskToday & (uint)eventBit) != 0) return null;
        if (!table.TryGetValue(eventBit, out var points)) return null;
        var newRelation = Clamp(relation + points.Relation);
        var newState = state == 0 && newRelation >= CloseEnoughRelation ? (byte)1 : state;
        return new ClosenessResult(newRelation, Clamp(love + points.Love), Clamp(friend + points.Friend), daily ? maskToday | (uint)eventBit : maskToday, newState);
    }

    private static byte Clamp(int value) => (byte)Math.Clamp(value, MinPoint, MaxPoint);
}
