using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public enum MorphRule
{
    None,
    WildMutation,
    WildChange,
    Beast,
}

public static class FormSkills
{
    public const int WildMutation = 1208;
    public const int WildChange = 1248;
    public const int ChainOfKnives = 1362;
    public const int SummonSoldier = 1382;
    public const int CardIdentification = 1393;
    public const int SummonGuardian = 1394;
    public const int UpThrow = 1475;
    public const int Bunker = 1491;
    public const int BunkerDo = 1492;
    public const int Beast = 1493;
    public const int BeastDo = 1494;
    public const int EscapeBunker = 1497;

    public const string ApplyRatioColumn = "ApplyRatio";
    public const string RatioColumn = "Ratio";
    public const string HpRemainColumn = "HpRemainRatio";
    public const string MpUnitColumn = "MpConUnit";
    public const string SecondColumn = "Second";
    public const string DamageRatioColumn = "DamageRatio";
    public const string MinCardColumn = "MinCard";
    public const string MaxCardColumn = "MaxCard";
    public const string CountColumn = "Count";
    public const string KnifeCountColumn = "KnifeCount";
    public const string KillNumColumn = "KillNum";
    public const string RadiusColumn = "Radius";
    public const string ApPlusRatioColumn = "ApPRatio";
    public const string DxMinusColumn = "DxPValue";
    public const string DpMinusRatioColumn = "DpMRatio";
    public const string HvMinusRatioColumn = "HvMRatio";
    public const string LkMinusRatioColumn = "LkMRatio";
    public const string ApMinusRatioColumn = "ApMRatio";
    public const string DxChangeColumn = "DxMValue";
    public const string DpPlusRatioColumn = "DpPRatio";
    public const string HvPlusRatioColumn = "HvPRatio";
    public const string BeastApColumn = "AP_Ratio";
    public const string BeastDxColumn = "DX_Ratio";
    public const string BeastDpColumn = "DP_Ratio";
    public const string BeastHvColumn = "HV_Ratio";
    public const string BeastLkColumn = "LK_Ratio";

    public const int MorphTrailingMs = 3000;
    public const int MorphDrainMs = 2000;
    public const int SoldierStrikeMs = 2000;
    public const int SoldierLifeMs = 3000;
    public const ushort SoldierWithoutCards = 3;
    public const int UpThrowVictims = 7;
    public const int UpThrowSlots = 6;
    public const int RandomMax = 0x7FFF;

    private const int BunkerDigStep = 15;
    private const int SoldierAvoidStep = 8;
    private const double SoldierCardDivisor = 6.0;
    private const double SoldierCardBase = 0.1;
    private const int KnifeDigShift = 4;
    private const int UpThrowDigStep = 10;

    private static readonly Dictionary<int, MorphRule> Morphs = new()
    {
        [WildMutation] = MorphRule.WildMutation,
        [WildChange] = MorphRule.WildChange,
        [BeastDo] = MorphRule.Beast,
    };

    public static MorphRule MorphOf(int actionId) => Morphs.TryGetValue(actionId, out var rule) ? rule : MorphRule.None;

    public static bool IsSelf(int actionId) => actionId is EscapeBunker or CardIdentification;

    public static bool IsGround(int actionId) => actionId == UpThrow;

    public static bool IsTargeted(int actionId) => actionId is SummonSoldier or ChainOfKnives;

    public static IReadOnlyList<(int Stat, int Delta)> MorphDeltas(MorphRule rule, SkillTableRow row, long ap, int dx, long lk, long dp, long hv)
    {
        ArgumentNullException.ThrowIfNull(row);
        float Column(string name) => row.Value(name) ?? 0f;
        static int Share(long stat, float ratio) => WireLimits.SaturateInt32(stat * (double)ratio);
        return rule switch
        {
            MorphRule.WildMutation => new[]
            {
                (SkillEffectKinds.StatAp, Share(ap, Column(ApPlusRatioColumn))),
                (SkillEffectKinds.StatDx, -(row.Amount(DxMinusColumn) ?? 0)),
                (SkillEffectKinds.StatDp, -Share(dp, Column(DpMinusRatioColumn))),
                (SkillEffectKinds.StatHv, -Share(hv, Column(HvMinusRatioColumn))),
                (SkillEffectKinds.StatLk, -Share(lk, Column(LkMinusRatioColumn))),
            },
            MorphRule.WildChange => new[]
            {
                (SkillEffectKinds.StatAp, -Share(ap, Column(ApMinusRatioColumn))),
                (SkillEffectKinds.StatDx, row.Amount(DxChangeColumn) ?? 0),
                (SkillEffectKinds.StatLk, -Share(lk, Column(LkMinusRatioColumn))),
                (SkillEffectKinds.StatDp, Share(dp, Column(DpPlusRatioColumn))),
                (SkillEffectKinds.StatHv, Share(hv, Column(HvPlusRatioColumn))),
            },
            MorphRule.Beast => new[]
            {
                (SkillEffectKinds.StatAp, Share(ap, Column(BeastApColumn))),
                (SkillEffectKinds.StatDx, -(int)Column(BeastDxColumn)),
                (SkillEffectKinds.StatDp, WireLimits.SaturateInt32(Share(dp, Column(BeastDpColumn)) - dp)),
                (SkillEffectKinds.StatHv, WireLimits.SaturateInt32(Share(hv, Column(BeastHvColumn)) - hv)),
                (SkillEffectKinds.StatLk, -Share(lk, Column(BeastLkColumn))),
            },
            _ => Array.Empty<(int Stat, int Delta)>(),
        };
    }

    public static long HpAfterMorph(long hp, float hpRemainRatio) => Math.Max(0L, WireLimits.Saturate(hp * (double)hpRemainRatio));

    public static long BunkerPool(long da, float ratio) => WireLimits.Saturate(da * BunkerDigStep * (double)ratio);

    public static int SoldierCards(int minCard, int maxCard, int random) =>
        WireLimits.SaturateInt32((maxCard - minCard + 1) * (random / (double)RandomMax) + minCard);

    public static long SoldierPower(long ap, long hv, float damageRatio, long cards)
    {
        var scaled = (float)((ap + hv * SoldierAvoidStep) * (double)damageRatio);
        return WireLimits.Saturate(scaled * (double)(float)(cards / SoldierCardDivisor + SoldierCardBase));
    }

    public static float KnifeMultiplier(bool powerThrowActive, int? powerThrowLevel, float? powerThrowRatio) =>
        powerThrowActive && powerThrowLevel is <= 0 && powerThrowRatio is { } ratio ? ratio : 1f;

    public static long KnifePower(long itemAp, float multiplier, long da, float ratio, int thrown, int count)
    {
        if (count == 0) return 1;
        var power = WireLimits.Saturate((itemAp * (double)multiplier + (da << KnifeDigShift)) * ratio * ((double)thrown / count));
        return power <= 0 ? 1 : power;
    }

    public static long UpThrowPower(long itemAp, float multiplier, long da, float ratio)
    {
        var power = WireLimits.Saturate((itemAp * (double)multiplier + da * UpThrowDigStep) * ratio);
        return power <= 0 ? 1 : power;
    }

    public static SkillHit SplitKnife(SkillHit hit, int thrown) =>
        thrown > 0 && hit.Damage > 0 && (hit.Kind & SkillDamage.KindMask) is not (SkillDamage.KindMiss or IncomingHits.KindGuarded)
            ? hit with { Damage = hit.Damage / thrown }
            : hit;
}
