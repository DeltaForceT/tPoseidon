using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public class NonresidentMonsterStrategy : BaseMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.NonresidentMonster;
    public override string ClassName => nameof(NonresidentMonsterStrategy);
}
