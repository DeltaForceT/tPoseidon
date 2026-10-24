using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters.Strategies;

namespace tPoseidon.Core.Game.Monsters;

public static class MonsterStrategyFactory
{
    public static IMonsterStrategy Create(MonsterAType atype)
    {
        return atype switch
        {
            MonsterAType.TestMonster => new TestMonsterStrategy(),
            MonsterAType.AggressiveMonster => new AggressiveMonsterStrategy(),
            MonsterAType.BossMonster => new BossMonsterStrategy(),
            MonsterAType.DoppelGanger => new DoppelGangerStrategy(),
            MonsterAType.DoppelGangerChild => new DoppelGangerChildStrategy(),
            MonsterAType.NonresidentMonster => new NonresidentMonsterStrategy(),
            MonsterAType.NonresidentBossMonster => new NonresidentBossMonsterStrategy(),
            MonsterAType.HarconMonster => new HarconMonsterStrategy(),
            MonsterAType.LowHarconAttackMonster => new LowHarconAttackMonsterStrategy(),
            MonsterAType.LowHarconAttackBosMonster => new LowHarconAttackBosMonsterStrategy(),
            MonsterAType.TestMonsterAlias => new TestMonsterStrategy(),
            _ => new TestMonsterStrategy(),
        };
    }

    public static MonsterAType FromByte(byte atype)
    {
        return atype switch
        {
            0 => MonsterAType.TestMonster,
            1 => MonsterAType.AggressiveMonster,
            3 => MonsterAType.BossMonster,
            4 => MonsterAType.DoppelGanger,
            5 => MonsterAType.DoppelGangerChild,
            6 => MonsterAType.NonresidentMonster,
            7 => MonsterAType.NonresidentBossMonster,
            8 => MonsterAType.HarconMonster,
            9 => MonsterAType.LowHarconAttackMonster,
            10 => MonsterAType.LowHarconAttackBosMonster,
            11 => MonsterAType.TestMonsterAlias,
            _ => MonsterAType.TestMonster,
        };
    }
}
