using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class DoppelGangerStrategy : BossMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.DoppelGanger;
    public override string ClassName => nameof(DoppelGangerStrategy);
}
