using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class ObjectHits
{
    public const int SharpBrandish = 1145;
    public const int ThunderBolt = 1157;
    public const int WindSlash = 1161;
    public const int FireBurn = 1318;
    public const int FireBurnDo = 1319;
    public const int SkyRay = 1285;
    public const int CelestialStrike = 1343;
    public const int GaleBreak = 1357;
    public const int DarkWhisper = 1380;
    public const int FlashAttacking = 1275;
    public const int FlashAttackingDo = 1276;
    public const int PowerFlashAttack = 1367;
    public const int WeaponThrow = 1111;
    public const int GellderHit = 1116;
    public const int CardThrow = 1150;
    public const int MultiGellder = 1165;
    public const int Kamikaze = 1479;
    public const int KamikazeDo = 1480;
    public const int ElectroShock = 1183;
    public const int ElectroShockSkill = 2113;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string RadiusColumn = "Radius";
    public const string KillNumColumn = "KillNum";
    public const string PowerColumn = "AP";
    public const string MagicPowerColumn = "MagicAP";
    public const string DarkPowerColumn = "M_AP";
    public const string SecondsColumn = "Seconds";
    public const string HpRemainColumn = "HpRemainRatio";
    public const string AfterBoomColumn = "AfterBoomTime";

    public const float SharpBrandishCone = 10f;
    public const int SharpBrandishLimit = 7;
    public const int ThunderBoltVictims = 10;
    public const int WindSlashVictims = 10;
    public const int FireBurnVictims = 3;
    public const int SkyRayLimit = 5;
    public const int CelestialVictims = 7;
    public const int GaleBreakVictims = 7;
    public const int DarkWhisperVictims = 5;
    public const int CardThrowVictims = 7;
    public const int MultiGellderVictims = 7;
    public const int KamikazeVictims = 7;

    public const int SharpBrandishLifeMs = 3001;
    public const int ThunderBoltLifeMs = 4001;
    public const int ElectroShockDelayMs = 1001;
    public const int WindSlashLifeMs = 1501;
    public const int SkyRayLifeMs = 3001;
    public const int GaleBreakLingerMs = 3001;
    public const int DarkWhisperFirstMs = 500;
    public const int DarkWhisperPeriodMs = 2000;
    public const int FireBurnPeriodMs = 2000;
    public const int WeaponThrowLifeMs = 1000;
    public const int GellderLifeMs = 2000;
    public const int SkyRayCasterHoldMs = 3000;

    public const int GellderCost = 100;
    public const int MultiGellderCost = 150;
    public const int GellderElement = 0x200;

    public const int FlashArrivalDistance = 80;
    public const float FlashSpeed = 600f;
    public const float PowerFlashSpeed = 750f;
    public const int FlashAcStep = 8;
    public const int PowerFlashAcStep = 16;

    public const int MagicLoss = 0x31;
    public const int LightMagicLoss = 0x19;
    public const int ElementTripled = 3;
    public const int CardHvStep = 8;
    public const int KamikazeFlags = 0x1000;
    public const int ResistedTargetInput = 1;
    public const int DefaultResultType = 0;
    public const int NoCelestialScan = 0;
    public const int UntilTargetGone = int.MaxValue;
    public const int WatchTickMs = (int)(SkillChecks.ServerTickSeconds * 1000);
    public const int DarkWhisperTicksPerRound = DarkWhisperPeriodMs / DarkWhisperFirstMs;

    private const int LuckHitBase = 90;
    private const float HitScale = 1.2f;
    private const int LowestHitChance = 20;
    private const int HighestHitChance = 100;

    private const double CelestialBase = 10.0;
    private const double CelestialDivisor = 20.0;

    public static bool IsSelf(int actionId) => actionId is WindSlash or DarkWhisper or Kamikaze || ObjectEffects.IsSelf(actionId) || UtilitySkills.IsSelf(actionId) || FormSkills.IsSelf(actionId) || KnockSkills.IsSelf(actionId) || ScrollSkills.IsSelf(actionId);

    public static bool IsGround(int actionId) => actionId is CelestialStrike or GaleBreak || ObjectEffects.IsGround(actionId) || UtilitySkills.IsGround(actionId) || FormSkills.IsGround(actionId) || ScrollSkills.IsGround(actionId);

    public static bool IsTargeted(int actionId) =>
        actionId is SharpBrandish or ThunderBolt or FireBurn or SkyRay or FlashAttacking or PowerFlashAttack
            or WeaponThrow or GellderHit or CardThrow or MultiGellder
        || ObjectEffects.IsTargeted(actionId) || FormSkills.IsTargeted(actionId);

    public static bool IsObjectHit(int actionId) => IsSelf(actionId) || IsGround(actionId) || IsTargeted(actionId);

    public static bool PaysGalders(int actionId) => actionId is GellderHit or MultiGellder;

    public static bool ThrowsItem(int actionId) => actionId is WeaponThrow or CardThrow;

    public static int GalderCostOf(int actionId) => actionId == MultiGellder ? MultiGellderCost : GellderCost;

    public static long ElementPower(int elementAttack, long power) => elementAttack * ElementTripled + power;

    public static long RatioPower(long ap, float ratio) => WireLimits.Saturate(ap * (double)ratio);

    public static long GalderPower(long cost, float ratio) => WireLimits.Saturate(cost * (double)ratio);

    public static long FlashPower(long ap, long ac, bool power) => ac * (power ? PowerFlashAcStep : FlashAcStep) + ap;

    public static long CardPower(long ap, long hv, float ratio) => WireLimits.Saturate((ap + hv * CardHvStep) * (double)ratio);

    public static long CardFalloff(long damage, float distance, int radius)
    {
        if (radius <= 0) return damage;
        var fall = (float)(1.0 - distance / radius);
        return WireLimits.Saturate(damage * (double)fall);
    }

    public static long CelestialPower(long ap, int scan)
    {
        var scaled = (float)(scan + CelestialBase);
        return ap + WireLimits.Saturate(ap * (double)scaled / CelestialDivisor);
    }

    public static (long Lost, long VictimDamage) KamikazeBlast(long hp, float hpRemainRatio, float ratio)
    {
        var kept = WireLimits.Saturate(hp * (double)hpRemainRatio);
        var lost = hp - kept;
        return (lost, WireLimits.Saturate(lost * (double)ratio));
    }

    public static float FlightSpeed(int actionId) => actionId == PowerFlashAttack ? PowerFlashSpeed : FlashSpeed;

    public static bool Lands(int attackerLk, int targetLk, float coefficient, int roll)
    {
        var chance = WireLimits.Saturate((attackerLk - targetLk + LuckHitBase) * HitScale * coefficient);
        return roll < Math.Clamp(chance, LowestHitChance, HighestHitChance);
    }

    public static bool DarkWhisperRoundDue(int periodCount) => (periodCount - 1) % DarkWhisperTicksPerRound == 0;

    public static SkillEffectKind LifeOf(int actionId, SkillPeriodicHit periodic) =>
        new(actionId, SkillEffectKinds.NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            Periodic = periodic, GrantsTm = false,
        };

    public static IReadOnlyList<ushort> SharpBrandishVictims(
        ushort casterEntityId, (int X, int Y) casterAt, AreaCandidate target, int radius, IEnumerable<AreaCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var victims = new List<ushort>();
        foreach (var candidate in candidates)
        {
            if (victims.Count >= SharpBrandishLimit) break;
            if (!Qualifies(candidate, casterEntityId, skipsFlyers: true)) continue;
            if (radius < SkillAreaRules.Distance(casterAt.X, casterAt.Y, candidate.X, candidate.Y)) continue;
            if (candidate.EntityId != target.EntityId
                && !SkillAreaShapes.InCone(casterAt.X, casterAt.Y, target.X, target.Y, candidate.X, candidate.Y, SharpBrandishCone))
            {
                continue;
            }
            victims.Add(candidate.EntityId);
        }
        return victims;
    }

    public static IReadOnlyList<ushort> TargetThenAround(
        ushort casterEntityId, AreaCandidate target, int radius, int limit, IEnumerable<AreaCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var victims = new List<ushort> { target.EntityId };
        foreach (var candidate in candidates)
        {
            if (victims.Count >= limit) break;
            if (candidate.EntityId == target.EntityId || !Qualifies(candidate, casterEntityId, skipsFlyers: true)) continue;
            if (radius < SkillAreaRules.Distance(target.X, target.Y, candidate.X, candidate.Y)) continue;
            victims.Add(candidate.EntityId);
        }
        return victims;
    }

    public static IReadOnlyList<ushort> Around(
        ushort casterEntityId, (int X, int Y) centre, int radius, int limit, bool skipsFlyers, IEnumerable<AreaCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var victims = new List<ushort>();
        foreach (var candidate in candidates)
        {
            if (victims.Count >= limit) break;
            if (!Qualifies(candidate, casterEntityId, skipsFlyers)) continue;
            if (radius < SkillAreaRules.Distance(centre.X, centre.Y, candidate.X, candidate.Y)) continue;
            victims.Add(candidate.EntityId);
        }
        return victims;
    }

    public static IReadOnlyList<ushort> SkyRayVictims(ushort casterEntityId, AreaCandidate target, int radius, IEnumerable<AreaCandidate> candidates)
    {
        var victims = Around(casterEntityId, (target.X, target.Y), radius, SkyRayLimit, skipsFlyers: false, candidates).ToList();
        if (victims.Count > 0 && !victims.Contains(target.EntityId)) victims[0] = target.EntityId;
        return victims;
    }

    public static int LimitOf(SkillTableRow action, int cap)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Math.Clamp(action.Amount(KillNumColumn) ?? 0, 0, cap);
    }

    private static bool Qualifies(AreaCandidate candidate, ushort casterEntityId, bool skipsFlyers)
    {
        if (candidate.IsDead || candidate.MonsterType == SkillAreaShapes.UntouchableMonsterType) return false;
        if (skipsFlyers && candidate.MonsterType == SkillAreaShapes.FlyerMonsterType) return false;
        return candidate.OwnerEntityId == 0 || candidate.OwnerEntityId == casterEntityId;
    }
}
