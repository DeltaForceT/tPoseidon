using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class TestMonsterStrategy : BaseMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.TestMonster;
    public override string ClassName => nameof(TestMonsterStrategy);
}
