using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public class NonresidentBossMonsterStrategy : BossMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.NonresidentBossMonster;
    public override string ClassName => nameof(NonresidentBossMonsterStrategy);
}
