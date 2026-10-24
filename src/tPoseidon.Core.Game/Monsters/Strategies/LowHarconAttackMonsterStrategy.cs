using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class LowHarconAttackMonsterStrategy : NonresidentMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.LowHarconAttackMonster;
    public override string ClassName => nameof(LowHarconAttackMonsterStrategy);
}
