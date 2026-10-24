using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class ObjectEffects
{
    public const int Insulator = 1269;
    public const int InsulatorDo = 1270;
    public const int SharpClaw = 1245;
    public const int FullHouse = 1280;
    public const int FullHouseHit = 1281;
    public const int Fluorescene = 1287;
    public const int FluoresceneDo = 1288;
    public const int IceCrystal = 1315;
    public const int ElectricField = 1316;
    public const int ElectricFieldHit = 1317;
    public const int DarkGhost = 1342;
    public const int PoisonBaggage = 1344;
    public const int PoisonBaggageDo = 1350;
    public const int SwampField = 1358;
    public const int SwampFieldDo = 1359;
    public const int DeadlyFunk = 1364;
    public const int PoisonAssault = 1383;
    public const int PoisonAssaultDo = 1384;
    public const int ElementUp = 1472;
    public const int ElementUpDo = 1473;
    public const int FastLoad = 1476;
    public const int SettleDown = 1486;
    public const int SettleDownDo = 1487;
    public const int DarkUp = 1488;
    public const int DarkUpDo = 1489;

    public const int SkunkBagSkill = 4010;
    public const int PoisonMasterySkill = 3105;
    public const int PoisonMasteryRowLevel = 11;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string RadiusColumn = "Radius";
    public const string SecondsColumn = "Seconds";
    public const string SecondColumn = "Second";
    public const string ConRatioColumn = "ConRatio";
    public const string PowerColumn = "AP";
    public const string FieldPowerColumn = "M_AP";
    public const string FieldChanceColumn = "ApplyRT";
    public const string SpeedDownColumn = "SpeedDown";
    public const string DamagePerSecColumn = "DamagePerSec";
    public const string PoisonRatioColumn = "PosionRatio";
    public const string KillNumColumn = "KillNum";

    public const int ElectricFieldVictims = 5;
    public const int ElectricFieldPeriodMs = 2000;
    public const int SwampFieldVictims = 7;
    public const int SpreadMembers = 5;
    public const int DeadlyFunkVictims = 5;
    public const int FluoresceneVictims = 10;
    public const int FullHouseVictims = 5;
    public const int FullHouseDelayMs = 1000;
    public const int FullHouseCards = 5;
    public const int SharpClawBleedMs = 2000;
    public const int PoisonBaggagePeriodMs = 1000;
    public const int PoisonFlags = 0x800;
    public const int WholePoisonDamage = 1;
    public const int EarthMagicLoss = 0x19;
    public const int WaterMagicLoss = 0x31;
    public const float SpeedDownScale = 0.01f;

    private const double IceDropDivisor = 2.0;
    private const double IceDropCap = 95.0;
    private const int ElectricMpStep = 120;
    private const double ThrowDigStep = 16.0;
    private const int CardStep = 8;
    private const double ReducedShare = 0.1f;

    public static bool IsSelf(int actionId) => actionId is Fluorescene or FullHouse;

    public static bool IsGround(int actionId) => actionId == DeadlyFunk;

    public static bool IsTargeted(int actionId) => actionId is ElectricField or SharpClaw or PoisonBaggage or PoisonAssault;

    public static int IceResistDrop(int ownerMagic, float ratio) => (int)Math.Min(ownerMagic * (double)ratio / IceDropDivisor, IceDropCap);

    public static int ElectricFieldLoss(int casterMp) => -(casterMp / ElectricMpStep);

    public static int WeightPower(int weight, float factor) => (int)(weight * (double)factor);

    public static int PoisonTickDamage(int damage, float ratio) => damage > 0 ? (int)(damage * (double)ratio) : damage;

    public static int ThrownWeaponPower(ItemParamEntry item, float multiplier)
    {
        ArgumentNullException.ThrowIfNull(item);
        var power = item.Type == ThrowSkills.ThrowItemType ? (int)(item.Ap * multiplier) : item.Weight / 2;
        return Math.Max(1, power);
    }

    public static int PoisonAssaultPower(int weaponPower, int da, float ratio) => (int)(weaponPower + da * ThrowDigStep * ratio);

    public static int PoisonAssaultTick(int weaponPower, int da, float ratio, float poisonRatio) =>
        (int)(poisonRatio * ((weaponPower + da * ThrowDigStep) * ratio));

    public static int FullHousePower(int ap, int hv, int lk, float ratio) => (int)((ap + CardStep * hv + CardStep * lk) * (double)ratio);

    public static int FullHouseReduced(int power, int dp) => (int)((power - dp) * ReducedShare);

    public static Effects.SkillEffectMode SharpClawMode(int hitKind, Effects.SkillEffectMode rolled) => (hitKind & SkillDamage.KindMask) switch
    {
        SkillDamage.KindMiss => Effects.SkillEffectMode.Failed,
        Effects.IncomingHits.KindGuarded or Effects.IncomingHits.KindBlockedByGuard or Effects.IncomingHits.KindAbsorbed => Effects.SkillEffectMode.Resisted,
        _ => rolled,
    };

    public static bool StopsPoison(int hitKind, int damage) =>
        damage == 0 || (hitKind & SkillDamage.KindMask) is SkillDamage.KindMiss or Effects.IncomingHits.KindGuarded or Effects.IncomingHits.KindBlockedByGuard or Effects.IncomingHits.KindAbsorbed;

    public static bool StunsAfterFullHouse(int hitKind) =>
        (hitKind & SkillDamage.KindMask) is not (SkillDamage.KindMiss or Effects.IncomingHits.KindGuarded or Effects.IncomingHits.KindBlockedByGuard or Effects.IncomingHits.KindAbsorbed);
}
