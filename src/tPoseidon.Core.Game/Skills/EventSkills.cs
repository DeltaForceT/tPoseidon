using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class EventSkills
{
    public const int PerryFlame = 1121;
    public const int WhitePearl = 1124;
    public const int LoveChoco = 1137;
    public const int WhiteSplash = 1179;
    public const int Butterfly = 1180;
    public const int GhostTemptation = 1181;
    public const int SurprisePotpourri = 1197;
    public const int FireCracker = 1209;
    public const int HeartSpring = 1222;
    public const int FirstAnniversary = 1223;
    public const int NumberOne = 1224;
    public const int SecondAnniversary = 1300;
    public const int DancingCora = 1291;
    public const int BubbleFlame = 1307;
    public const int CoupleHeartFirst = 1400;
    public const int CoupleHeartLast = 1421;
    public const int CoupleHeartDoOffset = 26;
    public const int GmCheerAttack = 1452;
    public const int GmCheerAttackDo = 1453;
    public const int GmDignityAttack = 1454;
    public const int GmCheerDefence = 1455;
    public const int GmCheerDefenceDo = 1456;
    public const int GmDignityDefence = 1457;
    public const int MoonRabbit = 1451;
    public const int HappyXmas = 1467;
    public const int DancingRudolf = 1468;
    public const int GmRegionCast = 1458;
    public const int GmDignityAttackSkill = 20002;
    public const int GmDignityDefenceSkill = 20003;
    public const int HeartSpringRadius = 200;
    public const int HeartFlightSpeed = 200;
    public const int HeartFlightLimitMs = 4000;
    public const int HeartDoMs = 4000;
    public const int HeartStepMs = 100;
    public const string RadiusColumn = "Radius";
    private const int Percent = 100;
    private const double MillisecondsPerSecond = 1000.0;

    private static readonly (string Column, int Stat)[] GmColumns =
    [
        ("ApUp", SkillEffectKinds.StatAp),
        ("AcUp", SkillEffectKinds.StatAc),
        ("MaUp", SkillEffectKinds.StatMa),
        ("MdUp", SkillEffectKinds.StatMd),
        ("DaUp", SkillEffectKinds.StatDa),
        ("LkUp", SkillEffectKinds.StatLk),
        ("DpUp", SkillEffectKinds.StatDp),
        ("HvUp", SkillEffectKinds.StatHv),
    ];

    public static IReadOnlyList<int> GmStats { get; } = GmColumns.Select(column => column.Stat).ToArray();

    public static bool IsSelf(int actionId) =>
        actionId is PerryFlame or WhitePearl or WhiteSplash or Butterfly or HeartSpring or BubbleFlame or NumberOne or MoonRabbit or HappyXmas;

    public static bool IsEmoteTarget(int actionId) => actionId is LoveChoco or GhostTemptation or SurprisePotpourri or DancingCora or DancingRudolf;

    public static bool IsPointEmote(int actionId) => actionId == FireCracker;

    public static bool IsCoupleHeart(int actionId) => actionId is >= CoupleHeartFirst and <= CoupleHeartLast;

    public static bool IsGmTarget(int actionId) => actionId is GmCheerAttack or GmCheerDefence;

    public static bool IsTargeted(int actionId) => IsEmoteTarget(actionId) || IsCoupleHeart(actionId) || IsGmTarget(actionId);

    public static bool IsGround(int actionId) => actionId is GmDignityAttack or GmDignityDefence;

    public static bool IsBuilt(int actionId) => IsSelf(actionId) || IsGround(actionId) || IsPointEmote(actionId);

    public static int HeartDoOf(int actionId) => actionId + CoupleHeartDoOffset;

    public static int GmDoOf(int actionId) => actionId is GmCheerAttack or GmDignityAttack ? GmCheerAttackDo : GmCheerDefenceDo;

    public static bool IsGmPercent(int skillId) => skillId is GmDignityAttackSkill or GmDignityDefenceSkill;

    public static int HeartFlightMs(double distance) =>
        (int)Math.Min(HeartFlightLimitMs, Math.Max(0.0, distance) / HeartFlightSpeed * MillisecondsPerSecond);

    public static int HeartLuck(int baseLuck, float ratio) => (int)(baseLuck * (double)ratio);

    public static IReadOnlyList<(int Stat, int Delta)> GmDeltas(SkillTableRow row, bool percentOfBase, IReadOnlyList<int> bases)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(bases);
        return GmColumns
            .Select(column =>
            {
                var field = (short)(row.Amount(column.Column) ?? 0);
                return (column.Stat, percentOfBase ? field * bases[column.Stat] / Percent : field);
            })
            .ToList();
    }
}
