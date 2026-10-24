using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public class BossMonsterStrategy : BaseMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.BossMonster;
    public override string ClassName => nameof(BossMonsterStrategy);
}
