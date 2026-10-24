using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Monsters.Strategies;
using Xunit;

namespace tPoseidon.Core.Game.Tests.Monsters;

public class MonsterStrategyFactoryTests
{
    [Theory]
    [InlineData(MonsterAType.TestMonster, typeof(TestMonsterStrategy))]
    [InlineData(MonsterAType.AggressiveMonster, typeof(AggressiveMonsterStrategy))]
    [InlineData(MonsterAType.BossMonster, typeof(BossMonsterStrategy))]
    [InlineData(MonsterAType.DoppelGanger, typeof(DoppelGangerStrategy))]
    [InlineData(MonsterAType.DoppelGangerChild, typeof(DoppelGangerChildStrategy))]
    [InlineData(MonsterAType.NonresidentMonster, typeof(NonresidentMonsterStrategy))]
    [InlineData(MonsterAType.NonresidentBossMonster, typeof(NonresidentBossMonsterStrategy))]
    [InlineData(MonsterAType.HarconMonster, typeof(HarconMonsterStrategy))]
    [InlineData(MonsterAType.LowHarconAttackMonster, typeof(LowHarconAttackMonsterStrategy))]
    [InlineData(MonsterAType.LowHarconAttackBosMonster, typeof(LowHarconAttackBosMonsterStrategy))]
    [InlineData(MonsterAType.TestMonsterAlias, typeof(TestMonsterStrategy))]
    public void Create_ReturnsExpectedStrategyType(MonsterAType atype, Type expected)
    {
        var strategy = MonsterStrategyFactory.Create(atype);
        Assert.IsType(expected, strategy);
        Assert.Equal(atype switch
        {
            MonsterAType.TestMonsterAlias => MonsterAType.TestMonster,
            _ => atype,
        }, strategy.Atype);
    }

    [Theory]
    [InlineData((byte)0, MonsterAType.TestMonster)]
    [InlineData((byte)1, MonsterAType.AggressiveMonster)]
    [InlineData((byte)3, MonsterAType.BossMonster)]
    [InlineData((byte)4, MonsterAType.DoppelGanger)]
    [InlineData((byte)5, MonsterAType.DoppelGangerChild)]
    [InlineData((byte)6, MonsterAType.NonresidentMonster)]
    [InlineData((byte)7, MonsterAType.NonresidentBossMonster)]
    [InlineData((byte)8, MonsterAType.HarconMonster)]
    [InlineData((byte)9, MonsterAType.LowHarconAttackMonster)]
    [InlineData((byte)10, MonsterAType.LowHarconAttackBosMonster)]
    [InlineData((byte)11, MonsterAType.TestMonsterAlias)]
    [InlineData((byte)2, MonsterAType.TestMonster)]
    [InlineData((byte)99, MonsterAType.TestMonster)]
    public void FromByte_MapsCorrectly(byte raw, MonsterAType expected)
    {
        Assert.Equal(expected, MonsterStrategyFactory.FromByte(raw));
    }
}
