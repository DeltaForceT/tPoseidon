using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class DoppelGangerChildStrategy : BaseMonsterStrategy
{
    public override MonsterAType Atype => MonsterAType.DoppelGangerChild;
    public override string ClassName => nameof(DoppelGangerChildStrategy);
}
