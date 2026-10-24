namespace tPoseidon.Core.Game.Combat;

public readonly record struct MeleeOutcome(int Damage, byte Result, byte ArmedNext);

public static class MeleeResolution
{
    public const byte ResultHit = 0;
    public const byte ResultMiss = 1;
    public const byte ResultBlock = 2;
    public const byte ResultCritical = 3;
    public const byte ResultStrongCritical = 4;
    public const byte ResultFinishingFlag = 0x80;
    public const byte ResultKindMask = 0x7F;

    public const byte NotArmed = 0;

    private const int PerMille = 1000;
    private const int PerCent = 100;
    private const int HighestChance = 90;
    private const int LowestLevelScaledChance = 10;
    private const double HvScale = 100.0;
    private const double LevelScaledWeight = 15.0;
    private const double DefenceShare = 0.75;
    private const double CriticalAttackShare = 1.5;
    private const double StrongCriticalFactor = 1.3;

    public static MeleeOutcome PlayerAgainstMonster(
        int attackerAp,
        int attackerAc,
        int attackerLk,
        byte armed,
        int targetDp,
        int targetHv,
        int targetLk,
        int targetHp,
        Random rng,
        int criticalBonus = 0,
        bool targetCannotEvade = false)
    {
        ArgumentNullException.ThrowIfNull(rng);

        var hit = rng.Next(PerMille) < 10 * Math.Min(99.0, 80.0 + 0.4 * (attackerAc - 50));
        var evade = rng.Next(PerMille) < 10 * Math.Min(20.0, 0.2 * targetHv) && !targetCannotEvade;
        var block = rng.Next(PerMille) < 10 * Math.Min(50.0, 0.5 * targetLk);

        if (armed == ResultCritical)
        {
            return Finish(Critical(attackerAp, targetDp), ResultCritical, targetHp);
        }
        if (armed == ResultStrongCritical)
        {
            return Finish(StrongCritical(attackerAp, targetDp), ResultStrongCritical, targetHp);
        }
        if (!hit)
        {
            var rearmed = rng.Next(PerCent) < Math.Min(90, attackerLk + 50);
            var armedNext = !rearmed
                ? NotArmed
                : attackerAc > targetHv + 60 ? ResultStrongCritical : ResultCritical;
            return new MeleeOutcome(0, ResultMiss, armedNext);
        }
        if (evade)
        {
            return new MeleeOutcome(0, ResultMiss, NotArmed);
        }
        if (block)
        {
            return new MeleeOutcome(0, ResultBlock, NotArmed);
        }

        if (rng.Next(PerCent) >= Math.Min(90, attackerLk + 30 + criticalBonus))
        {
            return Finish(Normal(attackerAp, targetDp), ResultHit, targetHp);
        }
        if (rng.Next(PerCent) >= Math.Min(90, attackerAc - 50))
        {
            return Finish(Critical(attackerAp, targetDp), ResultCritical, targetHp);
        }
        return Finish(StrongCritical(attackerAp, targetDp), ResultStrongCritical, targetHp);
    }

    public static MeleeOutcome MonsterAgainstPlayer(
        int attackerAp,
        int attackerAc,
        int attackerLk,
        int attackerLevel,
        bool levelScaledAccuracy,
        int targetDp,
        int targetHv,
        int targetLk,
        int targetLevel,
        int targetHp,
        bool targetProtected,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        bool hit;
        var evade = false;
        var block = false;
        if (levelScaledAccuracy)
        {
            var levelRatio = (double)targetLevel / Math.Max(1, attackerLevel);
            var chance = (int)(attackerAc
                - (targetHv / HvScale * LevelScaledWeight + levelRatio * LevelScaledWeight));
            hit = rng.Next(PerCent) < Math.Max(LowestLevelScaledChance, chance);
        }
        else
        {
            hit = rng.Next(PerCent) < Math.Min(HighestChance, attackerAc);
            evade = rng.Next(PerCent) < Math.Min(HighestChance, targetHv);
            block = rng.Next(PerCent) < Math.Min(HighestChance, targetLk);
        }

        if (targetProtected)
        {
            return new MeleeOutcome(0, ResultBlock, NotArmed);
        }
        if (!hit || evade)
        {
            return new MeleeOutcome(0, ResultMiss, NotArmed);
        }
        if (block)
        {
            return new MeleeOutcome(0, ResultBlock, NotArmed);
        }
        if (rng.Next(PerCent) >= Math.Min(HighestChance, attackerLk + 30))
        {
            return Finish(Normal(attackerAp, targetDp), ResultHit, targetHp);
        }
        if (rng.Next(PerCent) >= Math.Min(HighestChance, attackerAc - 50))
        {
            return Finish(Critical(attackerAp, targetDp), ResultCritical, targetHp);
        }
        return Finish(StrongCritical(attackerAp, targetDp), ResultStrongCritical, targetHp);
    }

    public static int Normal(int ap, int dp) => Math.Max(0, (int)(ap - DefenceShare * dp));

    public static int Critical(int ap, int dp) => Math.Max(1, (int)(CriticalAttackShare * ap - DefenceShare * dp));

    public static int StrongCritical(int ap, int dp) => (int)(Critical(ap, dp) * StrongCriticalFactor);

    private static MeleeOutcome Finish(int damage, byte kind, int targetHp)
        => new(damage, damage >= targetHp ? (byte)(kind | ResultFinishingFlag) : kind, NotArmed);
}
