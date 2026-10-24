using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public enum SkillPowerRule
{
    AttackTimesRatio,
    DigTimesRatio,
    MissingHpTimesRatio,
    StockTimesRatio,
    AttackAndAvoidThreeQuarters,
    LuckAndTargetLife,
    AvoidSixteenfold,
    AttackAndAvoidHalf,
    AttackAndLifeEighth,
    ShadowCopies,
    MagicPower,
    MagicPowerRandomTimes,
    AttackDigAndLuck,
    AttackAndDig,
    AttackLevelAndFlat,
    AttackWeaponDigAndLife,
    AttackTimesFireRatio,
}

public enum SkillRestart
{
    Always,
    WithoutGun,
    Never,
}

public enum SkillHitKind
{
    Physical,
    Magic,
}

public enum SkillTail
{
    Positions,
    PositionsAndFlag,
    Count,
    TargetOnly,
    None,
}

public sealed record SkillHitShape(SkillPowerRule Power, bool TakesOwner, SkillRestart Restart, int EnhancerSkillId = 0)
{
    public SkillHitKind Kind { get; init; } = SkillHitKind.Physical;
    public int Element { get; init; }
    public int MagicLoss { get; init; }
    public bool MagicLossLessAccuracy { get; init; }
    public SkillTail Tail { get; init; } = SkillTail.Positions;
    public bool TargetInHeader { get; init; }
    public bool FlatTm { get; init; }
    public bool KeepsEarlierAttack { get; init; } = true;
    public bool NeedsGun { get; init; }
    public bool CoefficientIsRatio { get; init; }
    public bool CoefficientIsOne { get; init; }
    public bool ReducedDefence { get; init; }
    public bool BaseDefence { get; init; }
    public int ResultFlags { get; init; }
    public int ElementSlot { get; init; } = ElementSlots.None;
    public int ElementStep { get; init; } = SkillHitShapes.DefaultElementStep;
    public bool MagicWhenElement { get; init; }
}

public static class SkillHitShapes
{
    public const int Shockwave = 1078;
    public const int BurningRave = 1080;
    public const int StoneStrike = 1090;
    public const int PowerBlow = 1094;
    public const int UpperSmash = 1100;
    public const int FinalBlow = 1123;
    public const int Uppercut = 1167;
    public const int PiercingWave = 1191;
    public const int BeastClaw = 1216;
    public const int ButtPlate = 1138;
    public const int VolleyKick = 1139;
    public const int LuckyFist = 1196;
    public const int WildNail = 1246;
    public const int RagingNail = 1346;
    public const int FlamingFist = 1335;
    public const int HeavyHit = 1347;
    public const int Shadow = 1262;
    public const int WhirlwindBlaze = 1158;
    public const int DripBomb = 1156;
    public const int AquaBomb = 1257;
    public const int Hellfire = 1163;
    public const int ArrowOfLight = 1164;
    public const int DarkLance = 1187;
    public const int Thunderbolt = 1356;
    public const int Scythe = 1282;
    public const int StaffOfAgony = 1283;
    public const int DragonStorm = 1264;
    public const int SuddenAttack = 1290;
    public const int CrackAttack = 1490;
    public const int RazorGale = 1321;

    public const int TitaniumWrist = 4011;

    public const int FlyHammer = 1539;
    public const int HammerAp = 1551;

    public const string RatioColumn = "Ratio";
    public const string ApplyRatioColumn = "ApplyRatio";
    public const string PowerColumn = "AP";
    public const string CountColumn = "Count";
    public const string DefenceColumn = "DisDefenceRatio";
    public const string ThunderPowerColumn = "M_AP";
    public const string ThunderCountColumn = "MaxCount";
    public const string ThunderCoefficientColumn = "ApplyRT";
    public const string FlatAttackColumn = "addAP";
    public const string LifeRatioColumn = "HPRatio";
    public const int LevelCap = 30;

    public const int FireElement = 1;
    public const int WaterElement = 2;
    public const int WindElement = 4;
    public const int ElectricElement = 0x10;
    public const int LightElement = 0x20;
    public const int DarkElement = 0x40;
    public const int GunElement = 0x100;
    public const int MagicLoss = 0x31;
    public const int ThunderMagicLoss = 0x19;
    public const int ShieldPiercingFlag = 0x2000;
    public const int DefaultElementStep = 3;
    public const int ScytheElementStep = 30;
    public const int AgonyElementStep = 15;
    public const int FlamingFistElementStep = 1;
    private const double FirePercent = 100.0;

    private const int DigPowerStep = 20;
    private const double MissingHpDivisor = 10.0;
    private const int StockFloor = 392;
    private const int StockStep = 8;
    private const int AvoidStep = 16;
    private const int LuckStep = 8;
    private const float TargetLifeShare = 0.1f;
    private const double ThreeQuarters = 0.75;
    private const double Half = 0.5;
    private const double LifeDivisor = 8.0;
    private const int ShadowDexterityBase = 11;
    private const int ShadowStep = 24;
    private const double PercentDivisor = 100.0;

    private static readonly Dictionary<int, SkillHitShape> Shapes = new()
    {
        [Shockwave] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: true, SkillRestart.Always),
        [BurningRave] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: false, SkillRestart.Always),
        [StoneStrike] = new(SkillPowerRule.DigTimesRatio, TakesOwner: false, SkillRestart.WithoutGun),
        [PowerBlow] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: false, SkillRestart.Always, TitaniumWrist),
        [UpperSmash] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: false, SkillRestart.Always) { KeepsEarlierAttack = false },
        [FinalBlow] = new(SkillPowerRule.MissingHpTimesRatio, TakesOwner: true, SkillRestart.Always),
        [Uppercut] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: true, SkillRestart.Always) { Tail = SkillTail.PositionsAndFlag },
        [PiercingWave] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: true, SkillRestart.Never),
        [BeastClaw] = new(SkillPowerRule.AttackTimesRatio, TakesOwner: true, SkillRestart.Never),
        [ButtPlate] = new(SkillPowerRule.StockTimesRatio, TakesOwner: true, SkillRestart.Never) { Element = GunElement, NeedsGun = true },
        [VolleyKick] = new(SkillPowerRule.AttackAndAvoidThreeQuarters, TakesOwner: true, SkillRestart.Always),
        [LuckyFist] = new(SkillPowerRule.LuckAndTargetLife, TakesOwner: false, SkillRestart.WithoutGun) { KeepsEarlierAttack = false },
        [WildNail] = new(SkillPowerRule.AvoidSixteenfold, TakesOwner: true, SkillRestart.Always),
        [RagingNail] = new(SkillPowerRule.AttackAndAvoidHalf, TakesOwner: true, SkillRestart.Always) { CoefficientIsOne = true, ReducedDefence = true },
        [FlamingFist] = new(SkillPowerRule.AttackTimesFireRatio, TakesOwner: true, SkillRestart.Always)
        {
            ElementSlot = ElementSlots.Fire, ElementStep = FlamingFistElementStep, MagicWhenElement = true,
        },
        [HeavyHit] = new(SkillPowerRule.AttackAndLifeEighth, TakesOwner: false, SkillRestart.Never) { FlatTm = true },
        [Shadow] = new(SkillPowerRule.ShadowCopies, TakesOwner: true, SkillRestart.Never) { Tail = SkillTail.Count, TargetInHeader = true },
        [WhirlwindBlaze] = Magic(FireElement) with { ElementSlot = ElementSlots.Fire },
        [DripBomb] = Magic(WaterElement) with { ElementSlot = ElementSlots.Water },
        [AquaBomb] = Magic(WaterElement) with { ElementSlot = ElementSlots.Water },
        [Hellfire] = Magic(DarkElement),
        [ArrowOfLight] = Magic(LightElement),
        [DarkLance] = Magic(DarkElement),
        [Thunderbolt] = new(SkillPowerRule.MagicPowerRandomTimes, TakesOwner: true, SkillRestart.Never)
        {
            Kind = SkillHitKind.Magic, Element = ElectricElement, MagicLoss = ThunderMagicLoss, ElementSlot = ElementSlots.Electric,
        },
        [Scythe] = Magic(DarkElement) with { Tail = SkillTail.None, TargetInHeader = true, ElementSlot = ElementSlots.Dark, ElementStep = ScytheElementStep },
        [StaffOfAgony] = Magic(DarkElement) with { Tail = SkillTail.None, TargetInHeader = true, ElementSlot = ElementSlots.Dark, ElementStep = AgonyElementStep },
        [DragonStorm] = Magic(FireElement) with
        {
            TakesOwner = false, Tail = SkillTail.None, TargetInHeader = true, FlatTm = true, ElementSlot = ElementSlots.Fire,
        },
        [SuddenAttack] = new(SkillPowerRule.AttackDigAndLuck, TakesOwner: false, SkillRestart.Never)
        {
            Tail = SkillTail.None, TargetInHeader = true, FlatTm = true, CoefficientIsRatio = true,
        },
        [CrackAttack] = new(SkillPowerRule.AttackAndDig, TakesOwner: false, SkillRestart.Never)
        {
            Tail = SkillTail.None, TargetInHeader = true, FlatTm = true, BaseDefence = true, ResultFlags = ShieldPiercingFlag,
        },
        [HammerAp] = new(SkillPowerRule.AttackLevelAndFlat, TakesOwner: true, SkillRestart.Always) { Tail = SkillTail.TargetOnly },
        [FlyHammer] = new(SkillPowerRule.AttackWeaponDigAndLife, TakesOwner: true, SkillRestart.Always) { Tail = SkillTail.TargetOnly },
        [RazorGale] = new(SkillPowerRule.MagicPower, TakesOwner: false, SkillRestart.Never)
        {
            Kind = SkillHitKind.Magic, Element = WindElement, MagicLoss = MagicLoss, MagicLossLessAccuracy = true,
            Tail = SkillTail.TargetOnly, FlatTm = true, ElementSlot = ElementSlots.Wind,
        },
    };

    private static SkillHitShape Magic(int element) => new(SkillPowerRule.MagicPower, TakesOwner: true, SkillRestart.Never)
    {
        Kind = SkillHitKind.Magic, Element = element, MagicLoss = MagicLoss,
    };

    public static SkillHitShape? Find(int actionId) => Shapes.TryGetValue(actionId, out var shape) ? shape : null;

    public static bool KeepsEarlierAttack(int actionId) => Find(actionId)?.KeepsEarlierAttack ?? true;

    public static int RestartDelayMs(int actionId, int attackMs, int attackReadyMs, int standardDelayMs) =>
        actionId == UpperSmash ? 2 * (attackMs + attackReadyMs) : standardDelayMs;

    public static int Power(SkillPowerRule rule, SkillAttacker attacker, float ratio) =>
        Power(rule, attacker, default, ratio, null, null);

    public static int Power(SkillPowerRule rule, SkillAttacker attacker, SkillTarget target, float ratio, SkillTableRow? action, Random? rng) => rule switch
    {
        SkillPowerRule.AttackTimesRatio => Truncate(attacker.Ap * (double)ratio),
        SkillPowerRule.DigTimesRatio => Truncate((attacker.Da + 1) * DigPowerStep * (double)ratio),
        SkillPowerRule.MissingHpTimesRatio => Truncate((attacker.Ap + (attacker.MaxHp - attacker.Hp) / MissingHpDivisor) * ratio),
        SkillPowerRule.StockTimesRatio => Truncate(((attacker.Da + attacker.Ac) * StockStep - StockFloor) * (double)ratio),
        SkillPowerRule.AttackAndAvoidThreeQuarters => Truncate((attacker.Ap + AvoidStep * attacker.Hv) * (double)ratio * ThreeQuarters),
        SkillPowerRule.LuckAndTargetLife => Truncate((LuckStep * attacker.Lk + (double)TargetLifeShare * target.MaxHp) * ratio),
        SkillPowerRule.AvoidSixteenfold => Truncate(AvoidStep * attacker.Hv * (double)ratio),
        SkillPowerRule.AttackAndAvoidHalf => Truncate((attacker.Ap + AvoidStep * attacker.Hv) * (double)ratio * Half),
        SkillPowerRule.AttackAndLifeEighth => Truncate((attacker.Ap + (float)(attacker.Hp / LifeDivisor)) * (double)ratio),
        SkillPowerRule.ShadowCopies => Truncate((attacker.Ap + (ShadowDexterityBase - attacker.Dx) * ShadowStep) * (double)ratio * (action?.Amount(CountColumn) ?? 0)),
        SkillPowerRule.MagicPower => (action?.Amount(PowerColumn) ?? 0) + attacker.ElementBonus,
        SkillPowerRule.AttackTimesFireRatio => Truncate(attacker.Ap * (double)((float)(attacker.ElementBonus / FirePercent) + ratio)),
        SkillPowerRule.MagicPowerRandomTimes => ((action?.Amount(ThunderPowerColumn) ?? 0) + attacker.ElementBonus)
            * ((rng ?? throw new ArgumentNullException(nameof(rng))).Next(Math.Max(1, action?.Amount(ThunderCountColumn) ?? 1)) + 1),
        SkillPowerRule.AttackDigAndLuck => Truncate((attacker.Ap + AvoidStep * (attacker.Da + attacker.Lk)) * (double)ratio),
        SkillPowerRule.AttackAndDig => Truncate((attacker.Ap + AvoidStep * attacker.Da) * (double)ratio),
        SkillPowerRule.AttackWeaponDigAndLife => Truncate(attacker.Ap + attacker.WeaponDa * (double)ratio + attacker.Hp * (double)(action?.Value(LifeRatioColumn) ?? 0f)),
        SkillPowerRule.AttackLevelAndFlat => Truncate(attacker.Ap + Math.Min(attacker.Level, LevelCap) * (double)ratio + (action?.Amount(FlatAttackColumn) ?? 0)),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "Unknown skill power rule."),
    };

    public static SkillHit Resolve(
        SkillHitShape shape, SkillTableRow action, SkillTableRow? enhancer, SkillAttacker attacker, SkillTarget target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(rng);
        var (ratio, applyRatio) = WithEnhancer(action, enhancer);
        var coefficient = shape.CoefficientIsOne ? 1f
            : shape.CoefficientIsRatio ? ratio
            : shape.Power == SkillPowerRule.MagicPowerRandomTimes ? action.Value(ThunderCoefficientColumn) ?? 0f
            : applyRatio;
        var power = Power(shape.Power, attacker, target, ratio, action, rng);
        if (shape.MagicWhenElement && attacker.ElementBonus > 0)
        {
            return SkillDamage.Magic(attacker, power, target, coefficient, 0, FireElement, rng) with { Flags = shape.ResultFlags };
        }
        if (shape.Kind == SkillHitKind.Magic)
        {
            var loss = shape.MagicLossLessAccuracy ? shape.MagicLoss - attacker.Ac : shape.MagicLoss;
            return SkillDamage.Magic(attacker, power, target, coefficient, loss, shape.Element, rng) with { Flags = shape.ResultFlags };
        }
        var defended = target;
        if (shape.ReducedDefence)
        {
            defended = target with { Dp = Truncate(target.Dp * ((action.Amount(DefenceColumn) ?? 0) / PercentDivisor)) };
        }
        else if (shape.BaseDefence)
        {
            defended = target with { Dp = target.BaseDp };
        }
        return SkillDamage.Physical(attacker, power, defended, coefficient, shape.Element, rng) with { Flags = shape.ResultFlags };
    }

    public static (float Ratio, float ApplyRatio) WithEnhancer(SkillTableRow action, SkillTableRow? enhancer)
    {
        ArgumentNullException.ThrowIfNull(action);
        var ratio = action.Value(RatioColumn) ?? 0f;
        var applyRatio = action.Value(ApplyRatioColumn) ?? 0f;
        if (enhancer is null) return (ratio, applyRatio);
        var enhancedApply = enhancer.Value(ApplyRatioColumn) ?? 0f;
        return (ratio + (enhancer.Value(RatioColumn) ?? 0f), enhancedApply > 0f ? enhancedApply : applyRatio);
    }

    private static int Truncate(double value) => (int)Math.Clamp(Math.Truncate(value), int.MinValue, int.MaxValue);
}
