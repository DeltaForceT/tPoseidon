namespace tPoseidon.Core.Data;

public interface IMonsterEntityProvider
{
    IReadOnlyCollection<MonsterEntity> GetByMap(ushort mapId);

    IReadOnlyCollection<MonsterEntity> GetByScene(ushort mapId, ushort instanceKey);
}
