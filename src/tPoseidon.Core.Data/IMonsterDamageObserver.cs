namespace tPoseidon.Core.Data;

public interface IMonsterDamageObserver
{
    void Damaged(MonsterEntity monster, long damage);

    void Absorbed(MonsterEntity monster, long healed);
}
