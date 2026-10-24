namespace tPoseidon.Core.Data;

public sealed record CoupleMember(ulong CharacterId, bool Female, long EventBitmask, byte DivorceState, string Name, int Job);

public sealed record CoupleRecord(
    ulong CoupleCode,
    byte State,
    byte RelationPoint,
    byte LovePoint,
    byte FriendPoint,
    byte AdviceIndex1,
    byte AdviceIndex2,
    int RingItemIndex,
    int DressItemIndex,
    short TitleIndex,
    DateTimeOffset BeginCoupleDate,
    DateTimeOffset BeginMarriageDate,
    IReadOnlyList<CoupleMember> Members,
    DateTimeOffset RecentUpdate = default)
{
    public CoupleMember? Partner(ulong characterId) => Members.FirstOrDefault(member => member.CharacterId != characterId);
}

public sealed record CoupleCharInfo(ulong CoupleCode, short DivorceCount, short WeddingInCount);

public enum CoupleStoreError
{
    None = 0,
    AlreadyCoupled = 101,
    StoreFailed = 102,
}

public sealed record CoupleRegistration(CoupleStoreError Error, ulong CoupleCode)
{
    public bool Succeeded => Error == CoupleStoreError.None;
}

public static class CoupleStates
{
    public const byte Joined = 0;
    public const byte CloseEnough = 1;
    public const byte OptionsChosen = 2;
    public const byte Married = 3;
    public const byte DivorceRequested = 4;
    public const byte DivorceCancelled = 5;
    public const byte Divorced = 6;
    public const byte CoupleDeleted = 7;
    public const byte DivorceDeleted = 8;
    public const byte NoCouple = 9;

    public const byte NoDivorce = 0;
    public const byte DivorceRequester = 1;
    public const byte DivorceReceiver = 2;

    public const byte StartRelationPoint = 0;
    public const byte StartLovePoint = 50;
    public const byte StartFriendPoint = 50;
}

public interface ICoupleRulesStore
{
    CoupleRegistration Register(ulong maleCharacterId, ulong femaleCharacterId, DateTimeOffset at);

    CoupleCharInfo? CharInfo(ulong characterId);

    CoupleRecord? Load(ulong coupleCode);

    CoupleRecord? LoadForCharacter(ulong characterId);

    bool RequestDivorce(ulong coupleCode, ulong requesterId, ulong receiverId, DateTimeOffset at);

    bool CancelDivorce(ulong coupleCode);

    bool ConfirmDivorce(ulong coupleCode, DateTimeOffset at);

    bool Delete(ulong coupleCode, byte state, IReadOnlyList<int> questIds, IReadOnlyList<int> skillIds);

    bool SetMarriageOptions(ulong coupleCode, int ringItemIndex, int dressItemIndex, short titleIndex, DateTimeOffset at);

    bool CompleteWedding(ulong coupleCode, DateTimeOffset at, IReadOnlyList<ulong> guests);

    void RecordCoupleSkill(ulong coupleCode, ulong characterId, bool female, int skillId, DateTimeOffset at);

    bool UpdateEventPoints(ulong coupleCode, ulong characterId, byte relation, byte love, byte friendship, long eventMask, byte state, DateTimeOffset at);
}
