using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class LowHarconAttackBosMonsterStrategy : NonresidentBossMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.LowHarconAttackBosMonster;
    public override string ClassName => nameof(LowHarconAttackBosMonsterStrategy);
}
