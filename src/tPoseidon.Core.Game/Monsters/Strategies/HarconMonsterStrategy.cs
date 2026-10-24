using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class HarconMonsterStrategy : BaseMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.HarconMonster;
    public override string ClassName => nameof(HarconMonsterStrategy);
}
