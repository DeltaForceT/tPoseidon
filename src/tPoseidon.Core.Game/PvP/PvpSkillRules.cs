using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Game.PvP;

public static class PvpSkillRules
{
    public const byte DuelMode = 2;
    public const byte GuildGameTeamZeroMode = 3;
    public const byte GuildGameTeamOneMode = 4;
    public const int HitFloor = 20;
    public const int HitCeiling = 90;
    public const int PhysicalHitBase = 40;
    public const int MagicHitBase = 90;
    public const int HitRollRange = 100;
    public const int MissKind = 1;

    public static bool HitsPlayers(int actionId) =>
        actionId is SkillActions.Punch or SkillActions.FairyOfMagic or SkillActions.ManaArrow
        || (actionId != SkillHitShapes.HeavyHit && SkillHitShapes.Find(actionId) is not null)
        || SkillAreaShapes.Find(actionId) is not null
        || HitsSinglePlayer(actionId)
        || actionId is ChainHits.BlazingStrike or ThrowSkills.FanOfKnives
        || HitsAimedPlayer(actionId);

    public static bool HitsAimedPlayer(int actionId) =>
        actionId is ObjectHits.SharpBrandish or ObjectHits.ThunderBolt or ObjectHits.SkyRay or ObjectHits.CardThrow
            or ObjectHits.GellderHit or ObjectHits.MultiGellder or ObjectHits.FireBurn
            or ObjectHits.FlashAttacking or ObjectHits.PowerFlashAttack
            or ObjectEffects.PoisonBaggage or ObjectEffects.PoisonAssault;

    public static bool HitsSinglePlayer(int actionId) =>
        (ChainHits.IsChain(actionId) && actionId != ChainHits.BlazingStrike)
        || PolarBearHits.CountOf(actionId) > 0
        || actionId is ThrowSkills.OnePair or ThrowSkills.FuriousGalderThrow or GunSkills.PowerShot or GunSkills.DoubleShot;

    public static bool GrantsSkillTm(byte casterPvpMode) => casterPvpMode == 0;

    public static bool Attackable(byte attackerMode, byte attackerRecordMode, byte targetMode, bool sameParty, bool duelOpponents) =>
        attackerMode switch
        {
            PkRules.PkZoneMode => !sameParty
                && attackerRecordMode is PkRules.RecordOff or PkRules.RecordSkirmish
                && targetMode == PkRules.PkZoneMode,
            DuelMode => targetMode == DuelMode && duelOpponents,
            GuildGameTeamZeroMode => targetMode == GuildGameTeamOneMode,
            GuildGameTeamOneMode => targetMode == GuildGameTeamZeroMode,
            _ => false,
        };

    public static bool MayTarget(
        byte attackerMode, byte attackerRecordMode, byte targetMode, bool sameParty, bool duelOpponents,
        bool attackerObserves, bool targetObserves, bool targetDead, long targetHp) =>
        Attackable(attackerMode, attackerRecordMode, targetMode, sameParty, duelOpponents)
        && !attackerObserves && !targetObserves && !targetDead && targetHp > 0;

    public static bool PhysicalHits(long attackerAc, long targetHv, int roll) =>
        roll < Math.Clamp(WireLimits.SaturateInt32(attackerAc - targetHv + PhysicalHitBase), HitFloor, HitCeiling);

    public static int MagicHitChance(long attackerLk, long targetLk) =>
        Math.Clamp(WireLimits.SaturateInt32(attackerLk - targetLk + MagicHitBase), HitFloor, HitCeiling);

    public static bool MagicHits(long attackerLk, long targetLk, int roll) =>
        roll < MagicHitChance(attackerLk, targetLk);

    public static bool Hits(int hitType, long attackerAc, long attackerLk, long targetHv, long targetLk, int roll) =>
        hitType == SkillDamage.PhysicalType
            ? PhysicalHits(attackerAc, targetHv, roll)
            : MagicHits(attackerLk, targetLk, roll);

    public static SkillHit Missed(SkillHit hit) => hit with { Damage = 0, Kind = MissKind, Flags = 0, Healed = 0 };

    public static SkillTarget TargetOf(long dp, long hv, long md, long lk, long hp, int level, long maxHp) =>
        new(dp, hv, md, lk, hp, level, Resist: 0, maxHp, BaseDp: dp);
}
