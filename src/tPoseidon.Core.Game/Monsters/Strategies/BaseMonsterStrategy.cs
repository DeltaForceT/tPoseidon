using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public abstract class BaseMonsterStrategy : IMonsterStrategy
{
    public abstract MonsterAType Atype { get; }
    public abstract string ClassName { get; }

    public virtual void OnSpawn(MonsterEntity mob) { }
    public virtual void OnTick(MonsterEntity mob, DateTimeOffset now) { }
    public virtual void OnDamaged(MonsterEntity mob, ushort attackerEntityId, int damage) { }
    public virtual void OnDeath(MonsterEntity mob, ushort killerEntityId) { }
}
