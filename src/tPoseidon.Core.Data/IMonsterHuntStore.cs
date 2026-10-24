namespace tPoseidon.Core.Data;

public interface IMonsterHuntStore
{
    MonsterHuntRecord? Find(ulong characterId);

    void Save(MonsterHuntRecord record);

    bool Delete(ulong characterId);
}
