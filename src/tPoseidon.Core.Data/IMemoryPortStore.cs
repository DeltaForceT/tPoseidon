namespace tPoseidon.Core.Data;

public interface IMemoryPortStore
{
    MemoryPort Upsert(MemoryPort entry);

    bool TryInsert(MemoryPort entry);

    MemoryPort? Find(ulong characterId, byte portType, byte slotNum);

    IReadOnlyList<MemoryPort> GetForCharacter(ulong characterId);

    IReadOnlyList<MemoryPort> GetForCharacterByType(ulong characterId, byte portType);

    bool Delete(ulong characterId, byte portType, byte slotNum);

    int DeleteAllForCharacter(ulong characterId);
}

public interface IPkInfoStore
{
    PkInfo GetOrCreate(ulong characterId);

    PkInfo IncrementKill(ulong characterId);

    PkInfo IncrementDeath(ulong characterId);

    PkInfo Save(PkInfo info);

    bool ResetTodayCounts(ulong characterId);

    int ResetAllTodayCounts();

    void AppendLog(PkInfo snapshot);

    IReadOnlyList<PkInfo> GetLog(ulong characterId, int limit = 50);
}

public interface IUniqueCharDutyInfoStore
{
    UniqueCharDutyInfo GetOrCreate(ulong characterId);

    UniqueCharDutyInfo Upsert(UniqueCharDutyInfo info);

    bool Delete(ulong characterId);
}

public interface ICharProfileStore
{
    CharProfile GetOrCreate(ulong characterId);

    CharProfile Upsert(CharProfile profile);

    bool Delete(ulong characterId);
}
