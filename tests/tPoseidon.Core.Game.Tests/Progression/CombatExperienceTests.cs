using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Game.Tests.Progression;

public sealed class CombatExperienceTests
{
    [Theory]
    [InlineData(100, 30, 50, 200, 15)]
    [InlineData(100, 80, 50, 200, 25)]
    [InlineData(100, 0, 50, 200, 0)]
    [InlineData(7, 1, 9, 9, 0)]
    public void Share_IsTheMonsterExpForTheHpTheHitTook(int monsterExp, int damage, int hpBefore, int maxHp, int share)
    {
        Assert.Equal(share, CombatExperience.Share(monsterExp, damage, hpBefore, maxHp));
    }

    [Theory]
    [InlineData(14, 5, 5, 16)]
    [InlineData(2, 5, 5, 2)]
    [InlineData(14, 5, 9, 16)]
    [InlineData(100, 10, 5, 32)]
    [InlineData(0, 5, 5, 1)]
    [InlineData(1, 5, 5, 1)]
    public void ForHit_AddsAFifthAndScalesDownForALowerMonster(int share, int playerLevel, int monsterLevel, int gain)
    {
        Assert.Equal(gain, CombatExperience.ForHit(share, playerLevel, monsterLevel));
    }

    [Theory]
    [InlineData(0, 1.0f)]
    [InlineData(4, 1.0f)]
    [InlineData(5, 1.2f)]
    [InlineData(15, 1.4f)]
    [InlineData(35, 1.8f)]
    [InlineData(60, 1.8f)]
    [InlineData(-1, -1.0f)]
    [InlineData(-5, -1.2f)]
    [InlineData(-25, -1.6f)]
    [InlineData(-60, -1.8f)]
    public void TmLevelFactor_StepsEveryTenLevelsOfDifference(int difference, float factor)
    {
        Assert.Equal(factor, CombatExperience.TmLevelFactor(10 + difference, 10), 5);
    }

    [Theory]
    [InlineData(20, 100, 100, 5, 5, 20)]
    [InlineData(20, 50, 100, 5, 5, 10)]
    [InlineData(20, 100, 100, 10, 5, 24)]
    [InlineData(20, 100, 100, 5, 10, 16)]
    [InlineData(20, 1, 100, 5, 5, 0)]
    public void TmForKill_CountsTheHpTheOwnerFoundAndTheLevelDifference(
        int monsterTm, int hpWhenOwned, int maxHp, int monsterLevel, int playerLevel, int tm)
    {
        Assert.Equal(tm, CombatExperience.TmForKill(monsterTm, hpWhenOwned, maxHp, monsterLevel, playerLevel));
    }
}
