using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills.Effects;
namespace tPoseidon.Core.Game.Skills;

public static class MonsterSkills
{
    public const int Heal = 1131;
    public const int ChainArrow = 1132;
    public const int MagicRing = 1133;
    public const int FireBreath = 1135;
    public const int ThunderBreath = 1136;
    public const int SharpBrandish = 1227;
    public const int Thunderbolt = 1229;
    public const int WindSlash = 1231;
    public const int FireExplosion = 1234;
    public const int RockSummons = 1235;
    public const int LightWave = 1238;
    public const int Siren = 1239;
    public const int DefenseParalysis = 1225;
    public const int PowerStun = 1226;
    public const int Berserk = 1228;
    public const int WaterNet = 1232;
    public const int ElectricShock = 1233;
    public const int WhirlWind = 1236;
    public const int CruelFlame = 1237;
    public const int FluentWaist = 1240;
    public const int Destruction = 1294;
    public const int ElectricField = 1316;
    public const int Vampiric = 1460;
    public const int Guard = 1463;
    public const int MagicPowerShare = 3;
    public const int BuffCancel = 1461;
    public const int BuffCancelVictims = 6;
    public const string MaxClearColumn = "MaxClear";
    public const string SubPowerColumn = "SubAP";
    public const int MonDarkBarrier = 1466;
    public const int Unshield = 1081;
    public const int DarkSlow = 1322;
    public const int Poison = 1464;
    public const int PoisonDo = 1465;
    public const int FreeMotionScroll = 1398;
    public const int WarpDefenseScroll = 1397;
    public const int MagicStorm = 1134;
    public const int EarthDivide = 1230;
    public const int MagicStormHeaderSkill = 2009;
    public const int EarthDivideVictims = 7;
    public const int StunCancelSkill = 1110;
    public const int Charging = 1292;
    public const int ChargingDo = 1293;
    public const int Warp = 1295;
    public const int ChargingVictims = 7;
    public const int ChargingMoveMs = 500;
    public const int WarpSearchRadius = 500;
    public const int WarpVictims = 7;
    public const int SelfDestructMs = 500;
    public const string MapIdColumn = "MapID";
    public const string DelayTimeColumn = "Delay";
    public const int StunnedCondition = 0xF;
    public const int ParalysedCondition = 0x10;
    public const int FrozenCondition = 1;
    public const int BoundCondition = 0xA;
    public const int MovementBlockingCondition = 1;
    public const int SkillBlockingCondition = 8;
    public const int ItemBlockingCondition = 4;
    private const int ChargingDistanceBase = 40;
    private const double ChargingDivisor = 800.0;

    public const int ElectricShockVictims = 5;
    public const int ElectricShockChildMs = 500;
    public const int ElectricShockObjectMs = 2000;
    public const int DestructionVictims = 7;
    public const int DestructionHoldMs = 500;
    public const int DotPeriodMs = 2000;
    public const int PoisonFirstMs = 700;
    public const int PoisonKind = 9;
    public const int PoisonFloorHp = 1;
    public const string SecondColumn = "Second";
    public const string SecondsColumn = "Seconds";
    public const string DelayColumn = "DelayTime";

    public const int LowestLevel = 1;
    public const int HighestLevel = SkillDefinition.HighestLevel;
    public const int ThunderboltVictims = 4;
    public const int WindSlashVictims = 5;
    public const int NoPropertyElement = 0x80;
    public const int MagicLoss = 0x31;
    public const int ThunderLoss = 0x19;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string PowerColumn = "AP";
    public const string RadiusColumn = "Radius";
    public const string CountColumn = "Count";
    public const string CastDelayColumn = "MonDelay";
    public const float DefaultCoolSeconds = 2f;

    private const float LifeShare = 0.1f;
    private const double DefenceShare = 0.5;

    private static readonly Dictionary<int, (int Action, int Skill)> Imitated = new()
    {
        [Heal] = (SkillActions.Cure, 2005),
        [ChainArrow] = (SkillActions.ManaArrow, 2001),
        [MagicRing] = (SkillAreaRules.ManaRing, 2008),
    };

    private static readonly Dictionary<int, (int Element, int Loss)> SingleMagic = new()
    {
        [FireExplosion] = (SkillActions.FireElement, MagicLoss),
        [RockSummons] = (SkillActions.EarthElement, MagicLoss),
        [LightWave] = (SkillActions.LightElement, MagicLoss),
    };

    public static MonsterSkillTiming Timing(float? castDelaySeconds, float? coolSeconds) => new(
        castDelaySeconds is > 0f ? (int)Math.Round(castDelaySeconds.Value * 1000.0) : 0,
        (int)Math.Round((coolSeconds ?? DefaultCoolSeconds) * 1000.0));

    public static bool IsBuilt(int actionId) =>
        actionId is Heal or ChainArrow or MagicRing or FireBreath or ThunderBreath or SharpBrandish or Thunderbolt or WindSlash or Siren
            or DefenseParalysis or PowerStun or Berserk or WaterNet or ElectricShock or WhirlWind or CruelFlame or FluentWaist
            or Destruction or Vampiric or Poison or Charging or Warp or MagicStorm or EarthDivide or Guard or Unshield or DarkSlow
            or MonDarkBarrier or SkillEffectKinds.MagicReflect or SkillEffectKinds.Subcutaneous or SkillEffectKinds.WindShield or SkillEffectKinds.DarkBarrier
            or ObjectHits.FlashAttacking or SkillActions.ManaArrow or SkillEffectKinds.WinkOfCharm or ElectricField or ObjectEffects.IceCrystal or SkillEffectKinds.Harden or ChainHits.QuadPunch or ChainHits.TetraPunch
            or BuffCancel or SkillActions.StoneThrowing or SupportSkills.FirstAid or GroundSkills.Earthquake or ObjectHits.GaleBreak or ObjectEffects.DeadlyFunk
            or SkillEffectKinds.Berserk or SkillEffectKinds.Bind or SkillEffectKinds.ChillingPrison or SkillEffectKinds.FireField or GunSkills.BreakBarrier
        || IsEmote(actionId) || IsStatEffect(actionId)
        || SingleMagic.ContainsKey(actionId) || HarconSkills.IsHarcon(actionId)
        || SkillAreaShapes.Find(actionId) is not null || SkillHitShapes.Find(actionId) is { NeedsGun: false };

    public static bool IsStatEffect(int actionId) =>
        SkillEffectKinds.Find(actionId) is { ChangesStat: true, Holds: false, ChangesSpeed: false, HalvesHits: false, Periodic: SkillPeriodicHit.None } kind
        && kind.SecondStat == SkillEffectKinds.NoStat && kind.ThirdStat == SkillEffectKinds.NoStat && kind.FlatColumn is null;

    public static bool IsObject(int actionId) =>
        actionId is DefenseParalysis or PowerStun or Berserk or WaterNet or ElectricShock or WhirlWind or CruelFlame or FluentWaist
            or Destruction or Vampiric or Poison or Charging or Warp or Unshield or DarkSlow
            or MonDarkBarrier or SkillEffectKinds.MagicReflect or SkillEffectKinds.Subcutaneous or SkillEffectKinds.WindShield or SkillEffectKinds.DarkBarrier
            or SkillEffectKinds.WinkOfCharm or ElectricField or ObjectEffects.IceCrystal or SkillEffectKinds.Harden or ChainHits.QuadPunch or ChainHits.TetraPunch
            or ObjectHits.GaleBreak or ObjectEffects.DeadlyFunk or SkillEffectKinds.Berserk or SkillEffectKinds.Bind or SkillEffectKinds.ChillingPrison
            or SkillEffectKinds.FireField or GunSkills.BreakBarrier
        || IsStatEffect(actionId);

    public static long BuffCancelDamage(long power, long step, int index) => WireLimits.Saturate(power + step * (double)index);

    public static bool IsEmote(int actionId) =>
        actionId is EventSkills.PerryFlame or EventSkills.WhitePearl or EventSkills.WhiteSplash or EventSkills.Butterfly;

    public static long ChargingPower(long pushed, long casterHp, float ratio) =>
        WireLimits.Saturate(unchecked((pushed + ChargingDistanceBase) * casterHp) * (double)ratio / ChargingDivisor);

    public static bool BlocksSkill(int condition, int skillId) => (condition & SkillBlockingCondition) != 0 && skillId != StunCancelSkill;

    public static bool BlocksMovement(int condition) => (condition & MovementBlockingCondition) != 0;

    public static bool BlocksItems(int condition) => (condition & ItemBlockingCondition) != 0;

    public static int DotElementOf(int actionId) => actionId == CruelFlame ? SkillActions.DarkElement : SkillActions.WindElement;

    public static long DotDamage(long firstHit, float ratio) => WireLimits.Saturate(firstHit * (double)ratio);

    public static float WaterNetDexterity(long casterMagic, float ratio) => (float)(casterMagic * (double)ratio / 100.0);

    public static long DestructionPower(long maxHp, float ratio) => WireLimits.Saturate(maxHp * (double)ratio);

    public static long PoisonDamage(long amount, long hp) => hp > PoisonFloorHp ? Math.Min(amount, hp - PoisonFloorHp) : 0;

    public static bool PoisonStrikeDue(int elapsedMs, int strikes, int periodMs) => elapsedMs >= PoisonFirstMs + strikes * periodMs;

    public static bool Castable(int actionId, int level) => level is >= LowestLevel and <= HighestLevel && IsBuilt(actionId);

    public static bool CastableOnMonster(int actionId, int level) =>
        Castable(actionId, level) && (actionId is ChainArrow or Thunderbolt || SingleMagicOf(actionId) is not null);

    public static (int Action, int Skill)? ImitationOf(int actionId) => Imitated.TryGetValue(actionId, out var imitation) ? imitation : null;

    public static (int Element, int Loss)? SingleMagicOf(int actionId) => SingleMagic.TryGetValue(actionId, out var magic) ? magic : null;

    public static (long Hp, bool Healed) Healing(long hp, long maxHp, float ratio)
    {
        var raised = WireLimits.Saturate(hp + maxHp * (double)ratio);
        var healed = raised > hp && hp < maxHp;
        return (Math.Min(raised, maxHp), healed);
    }

    public static long SirenPower(long hp, long dp, float ratio) => WireLimits.Saturate((hp * (double)LifeShare + dp * DefenceShare) * ratio);

    public static long RingFalloff(long damage, float distance, float radius)
    {
        if (radius <= 0f) return damage;
        var fall = 1.0f - distance / radius;
        return WireLimits.Saturate(damage * (double)fall);
    }
}

public readonly record struct MonsterSkillTiming(int CastMs, int CoolMs);
