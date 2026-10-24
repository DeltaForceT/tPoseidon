namespace tPoseidon.Core.Data;

public interface IAwakeningStore
{
    CharAwakening GetOrCreate(ulong characterId);

    CharAwakening Upsert(CharAwakening awakening);

    bool MarkLevelUp(ulong characterId, int charLevel);

    IReadOnlyList<CharAwakeningLevelUp> GetLevelUpLog(ulong characterId);

    bool Delete(ulong characterId);
}

public interface ISoulGuardianStore
{
    CharSoulGuardian CreateGuardian(ulong characterId, short guardianType, byte slotNum, string name, byte fType);

    CharSoulGuardian? FindGuardian(ulong guardianUid);

    IReadOnlyList<CharSoulGuardian> GetGuardiansForCharacter(ulong characterId);

    CharSoulGuardian UpdateGuardian(CharSoulGuardian guardian);

    bool DeleteGuardian(ulong guardianUid);

    void SetSlot(ulong characterId, byte slotNum, ulong? guardianUid);

    IReadOnlyList<CharSoulGuardianSlot> GetSlots(ulong characterId);
}

public interface ISoulSeedStore
{
    CharSoulSeed GetOrCreate(ulong characterId, int seedItemIndex);

    CharSoulSeed Upsert(CharSoulSeed soulSeed);

    bool Delete(ulong characterId);
}
