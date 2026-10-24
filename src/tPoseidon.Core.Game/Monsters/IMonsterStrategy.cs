using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters;

public interface IMonsterStrategy
{
    MonsterAType Atype { get; }
    string ClassName { get; }

    void OnSpawn(MonsterEntity mob);
    void OnTick(MonsterEntity mob, DateTimeOffset now);
    void OnDamaged(MonsterEntity mob, ushort attackerEntityId, int damage);
    void OnDeath(MonsterEntity mob, ushort killerEntityId);
}
