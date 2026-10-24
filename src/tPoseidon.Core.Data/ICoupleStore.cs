namespace tPoseidon.Core.Data;

public interface ICoupleStore
{
    Couple CreateCouple(ulong charAId, ulong charBId, bool charASex, bool charBSex);

    Couple? FindCouple(ulong coupleCode);

    Couple? FindCoupleForCharacter(ulong characterId);

    Couple UpdateCoupleHeader(Couple couple);

    bool DissolveCouple(ulong coupleCode);

    CoupleChar? FindCharRecord(ulong characterId);

    CoupleChar IncrementCounter(ulong characterId, CoupleCharCounter counter);

    IReadOnlyList<CoupleJoin> GetJoins(ulong coupleCode);

    bool UpdateDivorceState(ulong coupleCode, ulong characterId, byte divorceState);

    bool SetEventBitmask(ulong coupleCode, ulong characterId, long bitmask);
}

public enum CoupleCharCounter : byte
{
    Couple = 1,
    CoupleDelete = 2,
    Marriage = 3,
    Divorce = 4,
    WeddingIn = 5,
}
