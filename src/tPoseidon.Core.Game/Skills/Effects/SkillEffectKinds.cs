namespace tPoseidon.Core.Game.Skills.Effects;

public enum SkillEffectRule
{
    RatioOfBase,
    Flat,
    CasterAttack,
    CasterAttackSixteenthPlusFortyNine,
    CasterAttackSixteenth,
    CasterAvoidSixteenfold,
    FlatTwentieth,
    CasterAvoid,
    CasterMagicHundredthAdded,
    CasterMagicOverFortyNineSixteenfold,
    CasterDig,
    OwnerDefence,
    CasterAccuracy,
    BestPartyDefence,
    DoubleBase,
    FixedAtNegative,
    AttackRatioMinusOne,
    LowLifeAttack,
    OwnerMagicSixteenfold,
    CasterLuckDown,
    NoChange,
}

public enum SkillPeriodicHit
{
    None,
    DamageOverTime,
    AreaAroundOwner,
    Berserk,
    LowLifeCheck,
    HealOverTime,
    AuraScan,
    ShieldRegen,
    DelayedHold,
    DelayedStrike,
    RepeatingStrike,
    FieldScan,
    BuffCancel,
    StoredBurn,
    FixedBurn,
    DarkWhisperRound,
    GaleBurst,
    KamikazeBlast,
    FlashArrival,
    WatchTarget,
    PoisonTick,
    FixedPoison,
    ElectricFieldRound,
    FullHouseBurst,
    RevealScan,
    DashArrival,
    MineWatch,
    MorphDrain,
    SoldierStrike,
    KnockMove,
    MonsterDot,
    MonsterPoison,
    ThunderRider,
    MonsterWarp,
    SelfDestruct,
    CoupleHeart,
    HarconSlots,
}

public enum SkillInstant
{
    None,
    Cleanse,
    FirstAid,
    HeartsGrace,
    MedCure,
    ManaConvert,
    SelfCure,
}

public enum SkillEffectTail
{
    Mode,
    SecondsThenMode,
    TargetThenMode,
    Duration,
    FlagThenDuration,
    Empty,
    ModeThenThreshold,
    ModeThenAmount,
    RadiusPercent,
    CurrentThenMax,
    ChargeCount,
    ModeThenTarget,
    IntervalThenHeal,
    ModeRadiusPoint,
    RadiusOnly,
    ModeThenDuration,
    ModeThenPoint,
    ModeThenStrings,
}

public sealed record SkillEffectKind(
    int ActionId,
    int Stat,
    SkillEffectRule Rule,
    SkillEffectTail Tail,
    string AmountColumn,
    bool OnTarget,
    string DurationColumn = SkillEffectKinds.SecondsColumn,
    int MillisecondsPerUnit = SkillEffect.MillisecondsPerSecond)
{
    public string? FlatColumn { get; init; }

    public bool RollsChance { get; init; }

    public string ChanceColumn { get; init; } = SkillEffectKinds.ApplyRatioColumn;

    public bool Holds { get; init; }

    public string? TickColumn { get; init; }

    public SkillPeriodicHit Periodic { get; init; }

    public int SpreadsAs { get; init; }

    public string? ChargesColumn { get; init; }

    public string? SlowColumn { get; init; }

    public string? MemberAmountColumn { get; init; }

    public int PeriodMs { get; init; }

    public string? ThresholdColumn { get; init; }

    public string? SpreadRadiusColumn { get; init; }

    public int SpreadLimit { get; init; } = int.MaxValue;

    public IReadOnlyList<int> ResistsSkills { get; init; } = Array.Empty<int>();

    public string? RangeColumn { get; init; }

    public bool SilentStart { get; init; }

    public float SlowScale { get; init; } = 1f;

    public bool HalvesHits { get; init; }

    public bool GrantsTm { get; init; } = true;

    public int AuraChild { get; init; }

    public int AuraLimit { get; init; } = int.MaxValue;

    public bool AuraSkipsCaster { get; init; }

    public int AuraBolsterSkill { get; init; }

    public bool IsAuraChild { get; init; }

    public bool RaisesManaRecovery { get; init; }

    public bool LowersManaCost { get; init; }

    public bool SharesHits { get; init; }

    public bool AbsorbsHits { get; init; }

    public bool ReducesSkillHits { get; init; }

    public bool ReflectsMagic { get; init; }

    public string? ReflectColumn { get; init; }

    public int CounterElement { get; init; }

    public int RequiresEffect { get; init; }

    public byte ResistKind { get; init; }

    public string ResistColumn { get; init; } = SkillEffectKinds.ResistValueColumn;

    public int RemovesOnEnd { get; init; }

    public int SecondStat { get; init; } = SkillEffectKinds.NoStat;

    public string? SecondAmountColumn { get; init; }

    public int TickScale { get; init; } = 1;

    public int? FixedDurationMs { get; init; }

    public int HoldDelayMs { get; init; }

    public int MonsterSpread { get; init; }

    public int MonsterSpreadLimit { get; init; } = int.MaxValue;

    public bool SpreadOnly { get; init; }

    public SkillInstant Instant { get; init; }

    public string? HealColumn { get; init; }

    public int CleansesFromRow { get; init; }

    public int FieldChild { get; init; }

    public string? FieldLimitColumn { get; init; }

    public int FieldLimitCap { get; init; } = int.MaxValue;

    public bool FieldTracks { get; init; }

    public bool FollowsCaster { get; init; }

    public float? FixedCoefficient { get; init; }

    public bool StrikeGatesMode { get; init; }

    public int LowersResistElement { get; init; }

    public sbyte? HeaderClass { get; init; }

    public ushort HeaderState { get; init; }

    public string? TickSecondsColumn { get; init; }

    public bool BreaksBarrier { get; init; }

    public bool PreventsEvade { get; init; }

    public bool SkipsRoll { get; init; }

    public IReadOnlyList<int> StripsActions { get; init; } = Array.Empty<int>();

    public IReadOnlyList<int> CleansesActions { get; init; } = Array.Empty<int>();

    public bool StrikesOnStart { get; init; }

    public int StrikeElement { get; init; }

    public int StrikeMagicLoss { get; init; }

    public bool EndsWhenOwnerHit { get; init; }

    public int ThirdStat { get; init; } = SkillEffectKinds.NoStat;

    public bool StrikeLossLessLuck { get; init; }

    public bool ResistDropByOwnerMagic { get; init; }

    public int StrikeType { get; init; } = SkillDamage.MagicType;

    public string? StrikePowerColumn { get; init; }

    public bool AnnouncesSpread { get; init; }

    public string? TailFloatColumn { get; init; }

    public bool TailFloatInverted { get; init; }

    public bool HidesOwner { get; init; }

    public MorphRule Morph { get; init; }

    public int TrailingMs { get; init; }

    public bool PoolFromDig { get; init; }

    public bool TmOnSpread { get; init; }

    public bool ScrollStats { get; init; }

    public bool StrikeAddsElement { get; init; }

    public IReadOnlyList<int> ElementBoostSlots { get; init; } = Array.Empty<int>();

    public string? PartySpreadColumn { get; init; }

    public int PlayerCondition { get; init; }

    public int? RequiresWornSlot { get; init; }

    public bool GmStats { get; init; }

    public bool SilentEnd { get; init; }

    public string? ThirdAmountColumn { get; init; }

    public IReadOnlyList<int> Stats => new[] { Stat, SecondStat, ThirdStat }.Where(stat => stat != SkillEffectKinds.NoStat).ToArray();

    public bool ModeLast => Tail is SkillEffectTail.Mode or SkillEffectTail.SecondsThenMode or SkillEffectTail.TargetThenMode;

    public bool GuardsHits => ChargesColumn is not null;

    public string? HpLimitColumn { get; init; }

    public bool CarriesMode => ModeLast || Tail is SkillEffectTail.ModeThenThreshold or SkillEffectTail.ModeThenAmount or SkillEffectTail.ModeThenTarget;

    public bool ChangesStat => Stat != SkillEffectKinds.NoStat;
}

public static class SkillEffectKinds
{
    public const int StatAp = 0;
    public const int StatAc = 1;
    public const int StatDx = 2;
    public const int StatMp = 3;
    public const int StatMa = 4;
    public const int StatMd = 5;
    public const int StatWt = 6;
    public const int StatDa = 7;
    public const int StatLk = 8;
    public const int StatHp = 9;
    public const int StatDp = 10;
    public const int StatHv = 11;
    public const int StatCount = 12;
    public const int NoStat = -1;

    public const string RatioColumn = "Ratio";
    public const string DxColumn = "DX";
    public const string ApColumn = "AP";
    public const string SecondsColumn = "Seconds";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string MillisecondsColumn = "MSeconds";
    public const string DpRatioColumn = "DpRatio";
    public const string DpRevisionColumn = "DPRevision";
    public const string HpRatioColumn = "HPRatio";
    public const string TickColumn = "TicTime";
    public const string DpReductionColumn = "DPRatio";
    public const string GuardCountColumn = "Gnum";
    public const string SlowRatioColumn = "RunSpeed";
    public const string PartyDpRatioColumn = "PDpRatio";
    public const int BestShield = 1544;
    public const int CrazyHammerMode = 1533;
    public const int Berserk = 1171;
    public const int Harden = 1320;
    public const int SkunkBag = 1119;
    public const int Bind = 1154;
    public const int BindOnMonster = 1155;
    public const int WinkOfCharm = 1273;
    public const int ChillingPrison = 1355;
    public const int Adhesive = 1242;
    public const int AdhesiveOnMonster = 1243;
    public const int StunCancel = 1469;
    public const int ManaStormDelayMs = 900;
    public const int BurnPeriodMs = 2000;
    public const string StunTimeColumn = "StunTime";

    private static readonly int[] BarrierStripped = { 1118, 1130, 1129, 1311 };
    public const int FireField = 1353;
    public const int FireFieldOnMonster = 1354;
    public const int WaterArea = 1369;
    public const int ThunderArea = 1371;
    public const int FireArea = 1373;
    public const int EarthArea = 1375;
    public const int WindArea = 1377;
    public const int WhirlWind = 1186;
    public const int WhirlWindOnMonster = 1198;
    public const int DimensionOnMonster = 1485;
    public const int BuffCanclerOnMonster = 1478;
    public const int FieldScanMs = 1000;
    public const int FireFieldStrikeMs = 2000;
    public const int WhirlWindScanMs = 2000;
    public const int WhirlWindHitMs = 300;
    public const int FireFieldVictims = 7;
    public const int AreaVictims = 5;
    public const int WhirlWindVictims = 6;
    public const int FieldMagicLoss = 0x31;
    public const string KillNumColumn = "KillNum";
    public const string FieldPowerColumn = "M_AP";
    public const string RegiDownColumn = "RegiDown";
    public const string DelayColumn = "Delay";
    public const double RegiDownMagicScale = 400.0;
    public const double RegiDownFloor = 0.3;
    public const double RegiDownCap = 0.5;

    public static int ResistDrop(int casterMa, float regiDown, int baseResist)
    {
        var share = (casterMa / RegiDownMagicScale + RegiDownFloor) * regiDown;
        if (share > RegiDownCap) share = (float)RegiDownCap;
        return Math.Max(1, (int)(baseResist * share));
    }
    public const int EarthDivideDelayMs = 500;
    public const int EarthMagicLoss = 0x19;
    public const string AttackPowerColumn = "AP";
    public const int SelfCureDoCleansed = 5;
    public const string HealAmountColumn = "Heal";
    public const string IntervalColumn = "Interval";
    public const string TimeColumn = "Time";
    public const int SkunkBagDelayMs = 1100;
    public const int BindMonsters = 7;
    public const int PrisonMagicLoss = 0x31;
    public const string PrisonPowerColumn = "M_AP";
    public const string PrisonChanceColumn = "ApplyRT";
    public const string AvoidDownColumn = "HvDRatio";
    public const string LuckDownColumn = "LkDRatio";
    public const string DefenceDownColumn = "DpDRatio";
    public const double MonsterAvoidShare = 0.4;

    private static readonly int[] StunCancelled = { 1144, 1119, 1281, 1226, 1315, 1355, 1155, 1183, 1221, 1471 };
    public const int Shield = 1333;
    public const int MagicReflect = 1097;
    public const int Guard = 1309;
    public const int Subcutaneous = 1311;
    public const int WaterShield = 1247;
    public const int ElectricShield = 1250;
    public const int FireShield = 1251;
    public const int EarthShield = 1252;
    public const int WindShield = 1253;
    public const int DarkBarrier = 1254;
    public const int LightShield = 1255;
    public const int WaterCounter = 1337;
    public const int ThunderCounter = 1338;
    public const int FireCounter = 1339;
    public const int EarthCounter = 1340;
    public const int WindCounter = 1341;
    public const byte FireResist = 0x0B;
    public const byte WaterResist = 0x0C;
    public const byte WindResist = 0x0D;
    public const byte EarthResist = 0x0E;
    public const byte ElectricResist = 0x0F;
    public const byte LightResist = 0x10;
    public const byte DarkResist = 0x11;
    public const string ResistValueColumn = "Value";
    public const string RecoverySecondsColumn = "RecoverySec";
    public const string CountColumn = "Count";
    public const string ReflexRatioColumn = "ReflexRatio";
    public const string MagicDefenceRatioColumn = "MdPRatio";
    public const string DefenceRatioColumn = "DpPRatio";
    public const string CounterPowerColumn = "AP";
    public const double ShieldPoolScale = 15.0;
    public const float ShieldRegenShare = 0.1f;
    public const int CounterLifeMs = 1_000_000;
    public const int SecondsToMs = 1000;
    public const int HeartBeat = 1125;
    public const int HeartBeatOnMember = 1126;
    public const int ManaFastRecover = 1127;
    public const int ManaFastRecoverOnMember = 1128;
    public const int NpcBuffer = 1500;
    public const int BigShield = 1129;
    public const int BigShieldOnMember = 1130;
    public const int CheerUp = 1152;
    public const int CheerUpOnMember = 1153;
    public const int WhiteKnight = 1172;
    public const int WhiteKnightOnMember = 1173;
    public const int GoodLuck = 1175;
    public const int GoodLuckOnMember = 1176;
    public const int HawkEye = 1210;
    public const int HawkEyeOnMember = 1211;
    public const int MultiAgilityUp = 1212;
    public const int MultiAgilityUpOnMember = 1213;
    public const int TeamBolster = 4103;
    public const int AuraScanMs = 400;
    public const int AuraChildLifeMs = 2000;
    public const int BigShieldMembers = 7;
    public const string DamageRatioColumn = "DamageRatio";
    public const string DexterityColumn = "DX";
    public const float CheerUpRadiusUnit = 70.0f;
    public const double CheerUpPercent = 100.0;

    private static readonly float[] BigShieldPartyBonus = { 0f, 0f, 0.03f, 0.06f, 0.12f, 0.24f, 0.48f };

    public static float BigShieldBonus(int members) => members > 1 && members < BigShieldPartyBonus.Length ? BigShieldPartyBonus[members] : 0f;

    public static float CheerUpTail(int radius) => (float)(radius / CheerUpRadiusUnit * CheerUpPercent);
    public const int DarkSlow = 1322;
    public const string DownValueColumn = "DownValue";
    public const float PercentScale = 0.01f;
    public const int DeadAlive = 1351;
    public const int SaintPromise = 1361;
    public const int SaintOath = 1379;
    public const int SaintOathPromise = 1386;
    public const int PowerThrow = 1146;
    public const int GasMask = 1313;
    public const int InsulateArmor = 1314;
    public const int PreventSlow = 1328;
    public const int CustomizingGun = 1345;
    public const int SharpSense = 1217;
    public const int SharpSenseExtra = 1385;
    public const int BloodyOath = 1360;
    public const int EnergySword = 1368;
    public const int Boiling = 1391;
    public const int ThirdEye = 1390;
    public const int BerserkPeriodMs = 100;
    public const int BerserkDrainEvery = 10;
    public const int LowLifePeriodMs = 3000;
    public const int HealPeriodMs = 3000;
    public const int SaintOathMembers = 6;
    public const string SecondColumn = "Second";
    public const string AttackRatioColumn = "ApRatio";
    public const string LifeRatioColumn = "HPRatio";
    public const string LifePercentColumn = "HPPercent";
    public const string MagicRatioColumn = "MARatio";
    public const string RadiusColumn = "Radius";
    public const string RangeAmountColumn = "Range";
    public const string HpDropColumn = "HpDrop";
    public const string MpDropColumn = "MpDrop";
    public const string StressColumn = "StressUp";
    public const int BestShieldOnMember = 1545;
    public const int PartyGuard = 1528;
    public const int PartyGuardOnMember = 1529;
    public const byte GuardedResult = 6;
    public const int MillisecondsPerMillisecond = 1;

    private static readonly Dictionary<int, SkillEffectKind> Kinds = new SkillEffectKind[]
    {
        new(1079, StatAc, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1101, StatDx, SkillEffectRule.Flat, SkillEffectTail.Mode, DxColumn, OnTarget: false),
        new(1104, StatMa, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1092, StatDa, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1112, StatLk, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1095, StatHv, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1118, StatDp, SkillEffectRule.RatioOfBase, SkillEffectTail.SecondsThenMode, RatioColumn, OnTarget: false),
        new(1081, StatDp, SkillEffectRule.CasterAttack, SkillEffectTail.TargetThenMode, RatioColumn, OnTarget: true),
        new(1098, StatAc, SkillEffectRule.CasterAttackSixteenthPlusFortyNine, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1099, StatDa, SkillEffectRule.CasterAttackSixteenth, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1102, StatLk, SkillEffectRule.CasterAttackSixteenth, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1117, StatMd, SkillEffectRule.CasterAvoidSixteenfold, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1115, StatAc, SkillEffectRule.FlatTwentieth, SkillEffectTail.Mode, ApColumn, OnTarget: false),
        new(1122, StatMd, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1274, StatAp, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(1096, StatMa, SkillEffectRule.CasterAvoid, SkillEffectTail.TargetThenMode, RatioColumn, OnTarget: true),
        new(1106, StatDx, SkillEffectRule.CasterMagicHundredthAdded, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1107, StatAp, SkillEffectRule.CasterMagicOverFortyNineSixteenfold, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1109, StatHv, SkillEffectRule.CasterDig, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1110, StatDp, SkillEffectRule.OwnerDefence, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(1363, StatHv, SkillEffectRule.CasterAccuracy, SkillEffectTail.Mode, RatioColumn, OnTarget: true),
        new(NpcBuffer, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false),
        new(1524, StatAp, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false),
        new(1525, StatDa, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false),
        new(1526, NoStat, SkillEffectRule.NoChange, SkillEffectTail.FlagThenDuration, RatioColumn, OnTarget: true,
            DurationColumn: MillisecondsColumn, MillisecondsPerUnit: MillisecondsPerMillisecond)
        {
            RollsChance = true, ChanceColumn = RatioColumn, Holds = true,
        },
        new(1527, StatDp, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false) { FlatColumn = DpRevisionColumn },
        new(1534, StatDp, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false) { FlatColumn = DpRevisionColumn },
        new(1537, StatDp, SkillEffectRule.OwnerDefence, SkillEffectTail.Duration, DpReductionColumn, OnTarget: true)
        {
            RollsChance = true, TickColumn = TickColumn, Periodic = SkillPeriodicHit.DamageOverTime,
        },
        new(1540, StatDa, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false),
        new(1541, StatAp, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false),
        new(1542, StatDp, SkillEffectRule.RatioOfBase, SkillEffectTail.Duration, RatioColumn, OnTarget: false)
        {
            FlatColumn = DpRevisionColumn, HpLimitColumn = HpRatioColumn,
        },
        new(1543, StatDp, SkillEffectRule.CasterAttack, SkillEffectTail.Duration, RatioColumn, OnTarget: true)
        {
            RollsChance = true, SlowColumn = SlowRatioColumn,
        },
        new(1547, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Duration, RatioColumn, OnTarget: false)
        {
            TickColumn = TickColumn, Periodic = SkillPeriodicHit.AreaAroundOwner,
        },
        new(PartyGuard, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Duration, GuardCountColumn, OnTarget: false)
        {
            SpreadsAs = PartyGuardOnMember,
        },
        new(PartyGuardOnMember, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Duration, GuardCountColumn, OnTarget: false)
        {
            ChargesColumn = GuardCountColumn,
        },
        Aura(HeartBeat, HeartBeatOnMember, grantsTm: false),
        Aura(ManaFastRecover, ManaFastRecoverOnMember, grantsTm: false),
        Aura(BigShield, BigShieldOnMember, grantsTm: false) with { AuraLimit = BigShieldMembers, AuraBolsterSkill = TeamBolster },
        Aura(CheerUp, CheerUpOnMember, grantsTm: true),
        Aura(WhiteKnight, WhiteKnightOnMember, grantsTm: true) with { AuraSkipsCaster = true },
        Aura(GoodLuck, GoodLuckOnMember, grantsTm: true),
        Aura(HawkEye, HawkEyeOnMember, grantsTm: true),
        Aura(MultiAgilityUp, MultiAgilityUpOnMember, grantsTm: true),
        new(HeartBeatOnMember, StatAp, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false) { IsAuraChild = true },
        new(ManaFastRecoverOnMember, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            IsAuraChild = true, RaisesManaRecovery = true,
        },
        new(BigShieldOnMember, StatDp, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false) { IsAuraChild = true },
        new(CheerUpOnMember, NoStat, SkillEffectRule.NoChange, SkillEffectTail.RadiusPercent, RatioColumn, OnTarget: false)
        {
            IsAuraChild = true, LowersManaCost = true,
        },
        new(WhiteKnightOnMember, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, DamageRatioColumn, OnTarget: false)
        {
            IsAuraChild = true, SharesHits = true,
        },
        new(GoodLuckOnMember, StatLk, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false) { IsAuraChild = true },
        new(HawkEyeOnMember, StatAc, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false) { IsAuraChild = true },
        new(MultiAgilityUpOnMember, StatDx, SkillEffectRule.Flat, SkillEffectTail.Empty, DexterityColumn, OnTarget: false) { IsAuraChild = true },
        new(Shield, NoStat, SkillEffectRule.NoChange, SkillEffectTail.CurrentThenMax, RatioColumn, OnTarget: false)
        {
            AbsorbsHits = true, Periodic = SkillPeriodicHit.ShieldRegen, TickColumn = RecoverySecondsColumn, TickScale = SecondsToMs, GrantsTm = false,
        },
        new(MagicReflect, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false) { ReflectsMagic = true },
        new(Guard, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ChargeCount, CountColumn, OnTarget: false)
        {
            ChargesColumn = CountColumn, GrantsTm = false,
        },
        new(Subcutaneous, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, SecondColumn)
        {
            ReducesSkillHits = true,
        },
        ElementShield(WaterShield, WaterResist, WaterCounter),
        ElementShield(ElectricShield, ElectricResist, ThunderCounter),
        ElementShield(FireShield, FireResist, FireCounter),
        ElementShield(EarthShield, EarthResist, EarthCounter),
        ElementShield(WindShield, WindResist, WindCounter),
        new(DarkBarrier, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false)
        {
            ResistKind = DarkResist, ReflectColumn = ReflexRatioColumn,
        },
        new(LightShield, StatMd, SkillEffectRule.OwnerMagicSixteenfold, SkillEffectTail.Mode, MagicDefenceRatioColumn, OnTarget: false)
        {
            SecondStat = StatDp, SecondAmountColumn = DefenceRatioColumn, ResistKind = LightResist,
        },
        Counter(WaterCounter, SkillActions.WaterElement, WaterShield),
        Counter(ThunderCounter, SkillActions.ElectricElement, ElectricShield),
        Counter(FireCounter, SkillActions.FireElement, FireShield),
        Counter(EarthCounter, SkillActions.EarthElement, EarthShield),
        Counter(WindCounter, SkillActions.WindElement, WindShield),
        new(SkunkBag, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, SecondsColumn, OnTarget: true)
        {
            Holds = true, HoldDelayMs = SkunkBagDelayMs, Periodic = SkillPeriodicHit.DelayedHold, PeriodMs = SkunkBagDelayMs,
        },
        new(Bind, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false)
        {
            MonsterSpread = BindOnMonster, SpreadRadiusColumn = RadiusColumn, MonsterSpreadLimit = BindMonsters, EndsWhenOwnerHit = true,
        },
        new(BindOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Holds = true, GrantsTm = false,
        },
        new(WinkOfCharm, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true, SecondColumn),
        new(ChillingPrison, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenTarget, PrisonPowerColumn, OnTarget: true)
        {
            Holds = true, StrikesOnStart = true, StrikeGatesMode = true, ChanceColumn = PrisonChanceColumn,
            StrikeElement = SkillActions.WaterElement, StrikeMagicLoss = PrisonMagicLoss, StrikeAddsElement = true,
        },
        new(Adhesive, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            MonsterSpread = AdhesiveOnMonster, SpreadRadiusColumn = RadiusColumn, SpreadOnly = true,
        },
        new(AdhesiveOnMonster, StatHv, SkillEffectRule.CasterLuckDown, SkillEffectTail.Mode, AvoidDownColumn, OnTarget: true)
        {
            SecondStat = StatLk, SecondAmountColumn = LuckDownColumn, ThirdStat = StatDp, ThirdAmountColumn = DefenceDownColumn,
        },
        new(StunCancel, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            Instant = SkillInstant.Cleanse, CleansesActions = StunCancelled,
        },
        Instant(SupportSkills.FirstAid, SkillInstant.FirstAid),
        Instant(SupportSkills.HeartsGrace, SkillInstant.HeartsGrace),
        Instant(SupportSkills.MedCure, SkillInstant.MedCure),
        Instant(SupportSkills.ManaConvert, SkillInstant.ManaConvert),
        Instant(SupportSkills.SelfCure, SkillInstant.SelfCure),
        new(SupportSkills.SelfCureDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.IntervalThenHeal, HealAmountColumn, OnTarget: false, TimeColumn)
        {
            Periodic = SkillPeriodicHit.HealOverTime, TickColumn = IntervalColumn, TickScale = SecondsToMs, HealColumn = HealAmountColumn,
            CleansesFromRow = SelfCureDoCleansed,
        },
        GroundHit(GroundSkills.ManaStormHit, ManaStormDelayMs, SkillActions.NoPropertyElement, 0),
        GroundHit(GroundSkills.EarthDivideHit, EarthDivideDelayMs, SkillActions.EarthElement, EarthMagicLoss),
        GroundHit(GroundSkills.EarthquakeDo, EarthDivideDelayMs, SkillActions.EarthElement, EarthMagicLoss),
        new(FireField, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeRadiusPoint, RadiusColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.FieldScan, PeriodMs = FieldScanMs, FieldChild = FireFieldOnMonster,
            FieldLimitColumn = KillNumColumn, FieldLimitCap = FireFieldVictims, FieldTracks = true,
        },
        new(FireFieldOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, FieldPowerColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.RepeatingStrike, PeriodMs = FireFieldStrikeMs, FixedCoefficient = 1f,
            StrikeElement = SkillActions.FireElement, StrikeMagicLoss = FieldMagicLoss, GrantsTm = false, HeaderClass = 0, HeaderState = 1,
            StrikeAddsElement = true,
        },
        ElementArea(WaterArea, SkillActions.ElectricElement),
        ElementArea(ThunderArea, SkillActions.FireElement),
        ElementArea(FireArea, SkillActions.EarthElement),
        ElementArea(EarthArea, SkillActions.WindElement),
        ElementArea(WindArea, SkillActions.WaterElement),
        ElementAreaDo(WaterArea + 1, SkillActions.ElectricElement),
        ElementAreaDo(ThunderArea + 1, SkillActions.FireElement),
        ElementAreaDo(FireArea + 1, SkillActions.EarthElement),
        ElementAreaDo(EarthArea + 1, SkillActions.WindElement),
        ElementAreaDo(WindArea + 1, SkillActions.WaterElement),
        new(WhirlWind, NoStat, SkillEffectRule.NoChange, SkillEffectTail.RadiusOnly, RadiusColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.FieldScan, PeriodMs = WhirlWindScanMs, FieldChild = WhirlWindOnMonster,
            FieldLimitColumn = KillNumColumn, FieldLimitCap = WhirlWindVictims, FollowsCaster = true,
        },
        new(WhirlWindOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, AttackPowerColumn, OnTarget: true)
        {
            StrikesOnStart = true, FixedDurationMs = WhirlWindHitMs, StrikeElement = SkillActions.WindElement,
            StrikeMagicLoss = FieldMagicLoss, GrantsTm = false, StrikeAddsElement = true,
        },
        new(DimensionOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Holds = true, GrantsTm = false, HeaderClass = 1,
        },
        new(BuffCanclerOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.BuffCancel, TickSecondsColumn = DelayColumn, ChargesColumn = CountColumn,
            FixedDurationMs = int.MaxValue / 2, GrantsTm = false,
        },
        new(SkillRiders.PowerStun, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, SecondsColumn, OnTarget: true)
        {
            Holds = true, HeaderClass = 1,
        },
        new(SkillRiders.DefenseParalysis, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, SecondsColumn, OnTarget: true)
        {
            PreventsEvade = true, SkipsRoll = true, HeaderClass = 1,
        },
        new(SkillRiders.WaterNet, StatDx, SkillEffectRule.CasterMagicHundredthAdded, SkillEffectTail.Mode, RatioColumn, OnTarget: true)
        {
            RollsChance = true, HeaderClass = 1,
        },
        new(SkillRiders.CruelFlame, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.StoredBurn, PeriodMs = BurnPeriodMs, HeaderClass = 1, GrantsTm = false, StrikeElement = SkillActions.DarkElement,
        },
        new(ObjectEffects.SharpClaw, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ObjectEffects.ConRatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.StoredBurn, PeriodMs = ObjectEffects.SharpClawBleedMs, HeaderClass = 1, GrantsTm = false,
            StrikeType = SkillDamage.PhysicalType,
        },
        new(ObjectEffects.IceCrystal, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenTarget, RatioColumn, OnTarget: true)
        {
            StrikesOnStart = true, StrikeElement = SkillActions.WaterElement, StrikeMagicLoss = ObjectEffects.WaterMagicLoss, StrikeLossLessLuck = true,
            StrikeGatesMode = true, StrikePowerColumn = ObjectEffects.PowerColumn, StrikeAddsElement = true, Holds = true, HeaderClass = 1,
            LowersResistElement = SkillActions.ElectricElement, ResistDropByOwnerMagic = true,
        },
        new(ObjectEffects.SwampField, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeRadiusPoint, RadiusColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.FieldScan, PeriodMs = FieldScanMs, FieldChild = ObjectEffects.SwampFieldDo,
            FieldLimitColumn = KillNumColumn, FieldLimitCap = ObjectEffects.SwampFieldVictims, FieldTracks = true,
            ChanceColumn = ObjectEffects.FieldChanceColumn, StrikesOnStart = true, StrikeElement = SkillActions.EarthElement,
            StrikeMagicLoss = ObjectEffects.EarthMagicLoss, StrikeLossLessLuck = true, StrikePowerColumn = ObjectEffects.FieldPowerColumn,
        },
        new(ObjectEffects.SwampFieldDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, ObjectEffects.SpeedDownColumn, OnTarget: true)
        {
            SlowColumn = ObjectEffects.SpeedDownColumn, SlowScale = ObjectEffects.SpeedDownScale, HeaderClass = 1, GrantsTm = false,
        },
        new(ObjectEffects.ElementUp, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = ObjectEffects.ElementUpDo, SpreadRadiusColumn = RadiusColumn, SpreadLimit = ObjectEffects.SpreadMembers,
        },
        new(ObjectEffects.ElementUpDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, GrantsTm = false, ElementBoostSlots = ElementSlots.ElementUpSlots,
        },
        new(ObjectEffects.SettleDown, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = ObjectEffects.SettleDownDo, SpreadRadiusColumn = RadiusColumn, SpreadLimit = ObjectEffects.SpreadMembers, AnnouncesSpread = true,
        },
        new(ObjectEffects.SettleDownDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.RadiusPercent, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, GrantsTm = false, TailFloatColumn = RatioColumn, TailFloatInverted = true,
        },
        new(ObjectEffects.DarkUp, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = ObjectEffects.DarkUpDo, SpreadLimit = 0,
        },
        new(ObjectEffects.DarkUpDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, ElementBoostSlots = ElementSlots.DarkUpSlots,
        },
        new(ObjectEffects.Insulator, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = ObjectEffects.InsulatorDo, SpreadRadiusColumn = RadiusColumn,
        },
        new(ObjectEffects.InsulatorDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, ObjectEffects.SecondColumn)
        {
            HeaderClass = 0,
        },
        new(ObjectEffects.DarkGhost, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0,
        },
        new(ObjectEffects.FastLoad, NoStat, SkillEffectRule.NoChange, SkillEffectTail.RadiusPercent, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, GrantsTm = false, TailFloatColumn = RatioColumn,
        },
        new(ObjectEffects.FluoresceneDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true, ObjectEffects.SecondColumn)
        {
            HeaderClass = 1,
        },
        new(ObjectEffects.PoisonBaggageDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.PoisonTick, PeriodMs = ObjectEffects.PoisonBaggagePeriodMs, HeaderClass = 0, GrantsTm = false,
        },
        new(ObjectEffects.PoisonAssaultDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.FixedPoison, TickColumn = ObjectEffects.DamagePerSecColumn, TickScale = SkillEffect.MillisecondsPerSecond,
            HeaderClass = 0, GrantsTm = false,
        },
        new(UtilitySkills.Hiding, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false, UtilitySkills.SecondColumn)
        {
            HeaderClass = 0, HidesOwner = true,
        },
        new(UtilitySkills.OpticalEyes, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, Periodic = SkillPeriodicHit.RevealScan, PeriodMs = UtilitySkills.RevealScanMs,
        },
        new(FormSkills.WildMutation, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, Morph = MorphRule.WildMutation, TrailingMs = FormSkills.MorphTrailingMs,
            Periodic = SkillPeriodicHit.MorphDrain, PeriodMs = FormSkills.MorphDrainMs,
        },
        new(FormSkills.WildChange, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, Morph = MorphRule.WildChange, TrailingMs = FormSkills.MorphTrailingMs,
            Periodic = SkillPeriodicHit.MorphDrain, PeriodMs = FormSkills.MorphDrainMs,
        },
        new(FormSkills.Beast, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = FormSkills.BeastDo, SpreadLimit = 0, TmOnSpread = true,
        },
        new(FormSkills.BeastDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, Morph = MorphRule.Beast, TrailingMs = FormSkills.MorphTrailingMs,
            Periodic = SkillPeriodicHit.MorphDrain, PeriodMs = FormSkills.MorphDrainMs, StripsActions = new[] { FormSkills.WildMutation },
        },
        new(FormSkills.Bunker, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = FormSkills.BunkerDo, SpreadLimit = 0, TmOnSpread = true,
        },
        new(FormSkills.BunkerDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.CurrentThenMax, RatioColumn, OnTarget: false)
        {
            HeaderClass = 0, AbsorbsHits = true, PoolFromDig = true, GrantsTm = false,
        },
        new(FormSkills.SummonGuardian, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenDuration, RatioColumn, OnTarget: false, FormSkills.SecondColumn)
        {
            GrantsTm = false,
        },
        new(KnockSkills.ChargingHit, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenPoint, RatioColumn, OnTarget: true)
        {
            HeaderClass = -1, GrantsTm = false, Periodic = SkillPeriodicHit.KnockMove,
            FixedDurationMs = KnockSkills.ChargingMoveMs, PeriodMs = KnockSkills.ChargingMoveMs,
        },
        new(KnockSkills.PowerChargingDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenPoint, RatioColumn, OnTarget: true)
        {
            HeaderClass = -1, GrantsTm = false, Periodic = SkillPeriodicHit.KnockMove,
            FixedDurationMs = KnockSkills.PowerChargingMoveMs, PeriodMs = KnockSkills.PowerChargingMoveMs,
        },
        new(KnockSkills.MassiveAttack, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenPoint, RatioColumn, OnTarget: true)
        {
            HeaderClass = 1, GrantsTm = false, Periodic = SkillPeriodicHit.KnockMove,
            FixedDurationMs = KnockSkills.MassiveMoveMs, PeriodMs = KnockSkills.MassiveMoveMs,
        },
        new(ScrollSkills.StatUpScroll, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            SpreadsAs = ScrollSkills.StatUpScrollDo, SpreadRadiusColumn = ScrollSkills.RadiusColumn, SpreadLimit = ScrollSkills.PartyMembers,
            TmOnSpread = true, PartySpreadColumn = ScrollSkills.PartySkillTypeColumn,
        },
        new(ScrollSkills.StatUpScrollDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenStrings, RatioColumn, OnTarget: false)
        {
            ScrollStats = true,
        },
        new(ScrollSkills.RainOfHeart, StatLk, SkillEffectRule.Flat, SkillEffectTail.Empty, ScrollSkills.ValueColumn, OnTarget: false)
        {
            GrantsTm = false,
        },
        new(EventSkills.FirstAnniversary, StatLk, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            GrantsTm = false, RequiresWornSlot = tPoseidon.Core.Data.EquipmentSlot.Drill,
        },
        new(EventSkills.SecondAnniversary, StatLk, SkillEffectRule.RatioOfBase, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            GrantsTm = false, RequiresWornSlot = tPoseidon.Core.Data.EquipmentSlot.Drill,
        },
        new(EventSkills.GmCheerAttackDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            GrantsTm = false, GmStats = true,
        },
        new(EventSkills.GmCheerDefenceDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            GrantsTm = false, GmStats = true,
        },
        new(MonsterSkills.DefenseParalysis, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true, MonsterSkills.SecondColumn)
        {
            HeaderClass = 1, GrantsTm = false, PlayerCondition = MonsterSkills.ParalysedCondition,
        },
        new(MonsterSkills.PowerStun, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true, MonsterSkills.SecondColumn)
        {
            HeaderClass = 1, GrantsTm = false, PlayerCondition = MonsterSkills.StunnedCondition,
        },
        new(MonsterSkills.ChargingDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenPoint, RatioColumn, OnTarget: true)
        {
            GrantsTm = false, FixedDurationMs = MonsterSkills.ChargingMoveMs, PlayerCondition = MonsterSkills.FrozenCondition,
        },
        new(MonsterSkills.Warp, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            HeaderClass = -1, GrantsTm = false, Periodic = SkillPeriodicHit.MonsterWarp, SilentEnd = true,
        },
        new(MonsterSkills.Destruction, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            HeaderClass = -1, GrantsTm = false, Periodic = SkillPeriodicHit.SelfDestruct, SilentEnd = true,
            FixedDurationMs = MonsterSkills.SelfDestructMs, PeriodMs = MonsterSkills.SelfDestructMs,
        },
        new(MonsterSkills.Berserk, StatAp, SkillEffectRule.RatioOfBase, SkillEffectTail.Mode, RatioColumn, OnTarget: false, MonsterSkills.SecondsColumn)
        {
            HeaderClass = 0, GrantsTm = false,
        },
        new(MonsterSkills.WaterNet, StatDx, SkillEffectRule.Flat, SkillEffectTail.Mode, RatioColumn, OnTarget: true, MonsterSkills.SecondsColumn)
        {
            HeaderClass = 1, GrantsTm = false,
        },
        new(MonsterSkills.WhirlWind, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true, MonsterSkills.SecondsColumn)
        {
            HeaderClass = 1, GrantsTm = false, Periodic = SkillPeriodicHit.MonsterDot, PeriodMs = MonsterSkills.DotPeriodMs,
        },
        new(MonsterSkills.CruelFlame, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true, MonsterSkills.SecondsColumn)
        {
            HeaderClass = 1, GrantsTm = false, Periodic = SkillPeriodicHit.MonsterDot, PeriodMs = MonsterSkills.DotPeriodMs,
        },
        new(MonsterSkills.FluentWaist, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false, MonsterSkills.SecondsColumn)
        {
            HeaderClass = 0, GrantsTm = false,
        },
        new(MonsterSkills.Vampiric, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false, MonsterSkills.SecondColumn)
        {
            HeaderClass = 0, GrantsTm = false,
        },
        new(MonsterSkills.PoisonDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, MonsterSkills.PowerColumn, OnTarget: true, MonsterSkills.SecondColumn)
        {
            HeaderClass = 1, GrantsTm = false, Periodic = SkillPeriodicHit.MonsterPoison, PeriodMs = ObjectHits.WatchTickMs,
        },
        new(ObjectHits.ElectroShock, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true)
        {
            Holds = true, HeaderClass = 1, PlayerCondition = MonsterSkills.FrozenCondition,
        },
        new(ObjectEffects.FullHouseHit, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true, ObjectEffects.SecondColumn)
        {
            HeaderClass = 1, GrantsTm = false,
        },
        new(ObjectHits.FireBurnDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.FixedBurn, PeriodMs = ObjectHits.FireBurnPeriodMs, HeaderClass = 1, GrantsTm = false,
        },
        new(SkillRiders.FearOnMonster, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: true, SecondColumn)
        {
            HeaderClass = 1,
        },
        new(SkillRiders.UnableRecovery, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, SecondsColumn, OnTarget: true)
        {
            HeaderClass = 1, GrantsTm = false,
        },
        new(SkillRiders.ElectricSwordStun, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, StunTimeColumn, OnTarget: true, StunTimeColumn)
        {
            Holds = true, HeaderClass = 1, GrantsTm = false,
        },
        new(ThrowSkills.FourCardDo, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            FixedDurationMs = ThrowSkills.FourCardLifeMs, GrantsTm = false,
        },
        new(1301, StatAp, SkillEffectRule.Flat, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1302, StatMa, SkillEffectRule.Flat, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1303, StatLk, SkillEffectRule.Flat, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1304, StatHv, SkillEffectRule.Flat, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1305, StatDa, SkillEffectRule.Flat, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1397, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1398, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1399, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false),
        new(1423, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(GunSkills.BreakBarrier, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: true)
        {
            BreaksBarrier = true, StripsActions = BarrierStripped,
        },
        new(Harden, StatDx, SkillEffectRule.DoubleBase, SkillEffectTail.Mode, SecondsColumn, OnTarget: true)
        {
            HalvesHits = true,
        },
        new(DarkSlow, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, DownValueColumn, OnTarget: true)
        {
            SlowColumn = DownValueColumn, SlowScale = PercentScale,
        },
        new(Berserk, StatAp, SkillEffectRule.AttackRatioMinusOne, SkillEffectTail.Empty, AttackRatioColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.Berserk, PeriodMs = BerserkPeriodMs,
        },
        new(DeadAlive, StatAp, SkillEffectRule.LowLifeAttack, SkillEffectTail.ModeThenThreshold, RatioColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.LowLifeCheck, PeriodMs = LowLifePeriodMs, ThresholdColumn = LifePercentColumn, SilentStart = true,
        },
        new(SaintPromise, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenAmount, LifeRatioColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.HealOverTime, PeriodMs = HealPeriodMs,
        },
        new(SaintOathPromise, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeThenAmount, LifeRatioColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.HealOverTime, PeriodMs = HealPeriodMs,
        },
        new(SaintOath, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, LifeRatioColumn, OnTarget: false)
        {
            SpreadsAs = SaintOathPromise, SpreadRadiusColumn = RadiusColumn, SpreadLimit = SaintOathMembers,
        },
        new(PowerThrow, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false),
        new(GasMask, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, SecondColumn)
        {
            ResistsSkills = new[] { 4010 },
        },
        new(InsulateArmor, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, SecondColumn)
        {
            ResistsSkills = new[] { 2113, 5115 },
        },
        new(PreventSlow, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, SecondColumn)
        {
            ResistsSkills = new[] { 2215 },
        },
        new(CustomizingGun, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(SharpSense, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(SharpSenseExtra, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false),
        new(BloodyOath, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, MagicRatioColumn, OnTarget: false),
        new(EnergySword, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RangeAmountColumn, OnTarget: false)
        {
            RangeColumn = RangeAmountColumn,
        },
        new(Boiling, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, StressColumn, OnTarget: false, SecondColumn),
        new(ThirdEye, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, RatioColumn, OnTarget: false, SecondColumn),
        new(CrazyHammerMode, StatDx, SkillEffectRule.FixedAtNegative, SkillEffectTail.Duration, DxColumn, OnTarget: false),
        new(BestShield, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Duration, DpRatioColumn, OnTarget: false)
        {
            SpreadsAs = BestShieldOnMember,
        },
        new(BestShieldOnMember, StatDp, SkillEffectRule.BestPartyDefence, SkillEffectTail.Duration, DpRatioColumn, OnTarget: false)
        {
            MemberAmountColumn = PartyDpRatioColumn,
        },
    }.Concat(CoupleHeartKinds()).Concat(HarconKinds()).ToDictionary(kind => kind.ActionId);

    private static SkillEffectKind ElementArea(int actionId, int element) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.ModeRadiusPoint, RadiusColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.FieldScan, PeriodMs = FieldScanMs, FieldChild = actionId + 1,
            FieldLimitColumn = CountColumn, FieldLimitCap = AreaVictims, FieldTracks = true,
        };

    private static SkillEffectKind ElementAreaDo(int actionId, int element) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RegiDownColumn, OnTarget: true)
        {
            RollsChance = true, LowersResistElement = element, HeaderClass = 0,
        };

    private static SkillEffectKind GroundHit(int actionId, int delayMs, int element, int magicLoss) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, AttackPowerColumn, OnTarget: true)
        {
            Periodic = SkillPeriodicHit.DelayedStrike, PeriodMs = delayMs, FixedDurationMs = 2 * delayMs,
            StrikeElement = element, StrikeMagicLoss = magicLoss, StrikeAddsElement = true,
        };

    private static SkillEffectKind Instant(int actionId, SkillInstant instant) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false) { Instant = instant };

    private static SkillEffectKind ElementShield(int actionId, byte resist, int counter) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, ResistValueColumn, OnTarget: false)
        {
            ResistKind = resist, RemovesOnEnd = counter,
        };

    private static SkillEffectKind Counter(int actionId, int element, int shield) =>
        new(actionId, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Mode, CounterPowerColumn, OnTarget: false)
        {
            CounterElement = element, RequiresEffect = shield, FixedDurationMs = CounterLifeMs,
        };

    private static SkillEffectKind Aura(int parent, int child, bool grantsTm) =>
        new(parent, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            Periodic = SkillPeriodicHit.AuraScan, PeriodMs = AuraScanMs, AuraChild = child, GrantsTm = grantsTm,
        };

    private static IEnumerable<SkillEffectKind> HarconKinds() =>
        new[]
        {
            HarconSkills.Hurricane, HarconSkills.HurricaneStorm, HarconSkills.Pressure, HarconSkills.GiantPressure,
            HarconSkills.TimeBreak, HarconSkills.TimeBomb, HarconSkills.BrillantLight, HarconSkills.CronosImpact,
        }.Select(harcon => new SkillEffectKind(harcon, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
        {
            GrantsTm = false, Periodic = SkillPeriodicHit.HarconSlots, PeriodMs = HarconSkills.StepMs,
        });

    private static IEnumerable<SkillEffectKind> CoupleHeartKinds() =>
        Enumerable.Range(EventSkills.CoupleHeartFirst, EventSkills.CoupleHeartLast - EventSkills.CoupleHeartFirst + 1)
            .SelectMany(heart => new[]
            {
                new SkillEffectKind(heart, NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
                {
                    GrantsTm = false, Periodic = SkillPeriodicHit.CoupleHeart, PeriodMs = EventSkills.HeartStepMs,
                },
                new SkillEffectKind(EventSkills.HeartDoOf(heart), NoStat, SkillEffectRule.NoChange, SkillEffectTail.Empty, RatioColumn, OnTarget: false)
                {
                    GrantsTm = false, HeaderClass = -1, FixedDurationMs = EventSkills.HeartDoMs,
                },
            });

    public static SkillEffectKind? Find(int actionId) => Kinds.TryGetValue(actionId, out var kind) ? kind : null;
}
