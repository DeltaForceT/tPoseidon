using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.PvP;

public readonly record struct PkCounters(int KillToday, int KillTotal, int DeathToday, int DeathTotal);

public static class PvpHitRules
{
    public const byte LowestAttackMode = 1;
    public const byte HighestAttackMode = 4;
    public const int PlayerRollRange = 200;
    public const int PlayerHitCap = 180;
    public const int DodgeRollLow = 10;
    public const int DodgeRollHigh = 200;
    public const int DodgeCap = 170;
    public const double DefenceShare = 0.75;
    public const float OutsideTableFactor = 1.0f;

    public static readonly IReadOnlyList<float> ModeDamageFactors = new[] { 1.0f, 0.25f, 0.4f, 0.4f, 0.4f };

    public static float DamageFactor(byte attackerPvpMode) =>
        attackerPvpMode < ModeDamageFactors.Count ? ModeDamageFactors[attackerPvpMode] : OutsideTableFactor;

    public static int Damage(int attackerAp, int targetDp, float factor) =>
        Math.Max(0, (int)((int)(attackerAp - targetDp * DefenceShare) * factor));

    public static bool Dodges(int targetHv, int roll) => roll < Math.Min(targetHv, DodgeCap);

    public static MeleeOutcome Strike(
        int attackerAp, int attackerAc, byte attackerPvpMode, int targetDp, int targetHv, int targetHp, bool targetProtected, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var hit = Hits(attackerAc, rng.Next(PlayerRollRange));
        var dodged = Dodges(targetHv, rng.Next(DodgeRollLow, DodgeRollHigh));
        if (targetProtected) return new MeleeOutcome(0, MeleeResolution.ResultBlock, MeleeResolution.NotArmed);
        if (!hit || dodged) return new MeleeOutcome(0, MeleeResolution.ResultMiss, MeleeResolution.NotArmed);
        var damage = Damage(attackerAp, targetDp, DamageFactor(attackerPvpMode));
        var result = damage >= targetHp
            ? (byte)(MeleeResolution.ResultHit | MeleeResolution.ResultFinishingFlag)
            : MeleeResolution.ResultHit;
        return new MeleeOutcome(damage, result, MeleeResolution.NotArmed);
    }

    public static bool BreaksPeace(byte victimPvpMode, byte victimRecordMode, bool attackerIsPlayer) =>
        victimPvpMode == PkRules.PkZoneMode && victimRecordMode == PkRules.RecordPeace && attackerIsPlayer;

    public static bool CanAttackPlayer(byte attackerPvpMode) => attackerPvpMode is >= LowestAttackMode and <= HighestAttackMode;

    public static bool Hits(int attackerAc, int roll) => roll < Math.Min(attackerAc, PlayerHitCap);

    public static bool CreditsKill(byte victimPvpMode, byte victimRecordMode, bool killerIsPlayer) =>
        victimPvpMode == PkRules.PkZoneMode && killerIsPlayer && victimRecordMode != PkRules.RecordOff;

    public static bool AnnouncesKill(byte victimPvpMode, bool killerIsPlayer) => victimPvpMode == PkRules.PkZoneMode && killerIsPlayer;

    public static PkCounters VictimCredit(PkCounters victim, int totalDeathMax)
    {
        var today = victim.DeathToday + 1;
        return victim with { DeathToday = today, DeathTotal = today < totalDeathMax ? victim.DeathTotal + 1 : victim.DeathTotal };
    }

    public static PkCounters KillerCredit(PkCounters killer, int todayPkMax, int totalPkMax)
    {
        if (killer.KillToday >= todayPkMax) return killer;
        var today = killer.KillToday + 1;
        return killer with { KillToday = today, KillTotal = today < totalPkMax ? killer.KillTotal + 1 : killer.KillTotal };
    }
}
