namespace tPoseidon.Core.Game.Progression;

public static class CombatExperience
{
    public const int NormalAttackDivisor = 5;
    public const int SmallestGain = 1;

    private const float LowerMonsterFactor = 0.25f;
    private const float TmStep = 0.2f;
    private const float TmLargestFactor = 1.8f;
    private const int TmLevelBand = 10;
    private const int TmHalfBand = 5;

    public static int Share(int monsterExp, int damage, int monsterHpBefore, int monsterMaxHp)
    {
        if (monsterMaxHp == 0) return 0;
        var counted = monsterHpBefore < damage ? monsterHpBefore : damage;
        return unchecked(monsterExp * counted) / monsterMaxHp;
    }

    public static int ForHit(int share, int playerLevel, int monsterLevel, int divisor = NormalAttackDivisor)
    {
        var monster = monsterLevel < 1 ? 1 : monsterLevel;
        var player = playerLevel < 1 ? 1 : playerLevel;
        var levelFactor = player <= monster ? 1f : LowerMonsterFactor;
        var levelRatio = monster < player ? (float)((double)monster / player) : 1f;
        var gain = (float)((double)share * levelFactor * levelRatio + (double)share / divisor);
        if (gain < SmallestGain) gain = SmallestGain;
        return (int)gain;
    }

    public static float TmLevelFactor(int monsterLevel, int playerLevel)
    {
        var difference = monsterLevel - playerLevel;
        if (difference < 0)
        {
            var below = (float)((double)((difference - TmHalfBand) / TmLevelBand) * TmStep - 1.0);
            return below < -TmLargestFactor ? -TmLargestFactor : below;
        }
        var above = (float)((double)((difference + TmHalfBand) / TmLevelBand) * TmStep + 1.0);
        return above > TmLargestFactor ? TmLargestFactor : above;
    }

    public static int TmForKill(int monsterTm, int hpWhenOwned, int monsterMaxHp, int monsterLevel, int playerLevel)
    {
        var factor = TmLevelFactor(monsterLevel, playerLevel);
        var counted = (int)((double)hpWhenOwned / monsterMaxHp * monsterTm);
        return factor > 0f
            ? (int)((double)counted * factor)
            : (int)((double)counted / -factor);
    }
}
