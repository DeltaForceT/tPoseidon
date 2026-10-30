namespace tPoseidon.Core.Data;

public interface IMonsterDamageObserver
{
    void Damaged(MonsterEntity monster, long damage);
}
