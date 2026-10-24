using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public readonly record struct HealResult(long Hp, bool Healed);

public readonly record struct ManaConversion(bool Applied, long Hp, long Mp);

public static class SupportSkills
{
    public const int FirstAid = 1168;
    public const int HeartsGrace = 1323;
    public const int BasicHealing = 1219;
    public const int MedCure = 1259;
    public const int MedCureOnMember = 1260;
    public const int Revival = 1474;
    public const int ManaConvert = 1312;
    public const int SelfCure = 1482;
    public const int SelfCureDo = 1483;

    public const int BasicHealingEntries = 11;
    public const int MedCureEntries = 21;
    public const int MedCureMembers = 3;
    public const int NoAction = 1000;

    public const string RatioColumn = "Ratio";
    public const string RadiusColumn = "Radius";
    public const string HpRatioColumn = "HpRatio";
    public const string HpRestoreColumn = "fHPRestoreRatio";
    public const string MpRestoreColumn = "fMpRestoreRatio";
    public const string ExpRestoreColumn = "fExpRestoreRatio";
    public const string CleansedColumnPrefix = "ESAction";

    private const double FirstAidShare = 0.5;
    private const double GraceMagicScale = 40.0;

    public static bool TargetsPlayers(int actionId) =>
        actionId is SkillActions.Cure or BasicHealing or Revival or ScrollSkills.RainOfHeart || EventSkills.IsTargeted(actionId);

    public static bool TargetsDead(int actionId) => actionId == Revival;

    public static long FirstAidHeal(long ma, long lk, float ratio) => WireLimits.Saturate((ma + lk) * FirstAidShare * ratio);

    public static long GraceHeal(long memberMaxHp, long casterMa, float ratio) =>
        WireLimits.Saturate(memberMaxHp * (double)ratio) + WireLimits.Saturate(casterMa * (double)ratio * GraceMagicScale);

    public static HealResult Heal(long hp, long maxHp, long heal)
    {
        var raised = hp + heal;
        var healed = raised > hp && hp < maxHp;
        return new HealResult(Math.Clamp(Math.Min(raised, maxHp), 0L, long.MaxValue), healed);
    }

    public static ManaConversion ConvertMana(long hp, long maxHp, long mp, long maxMp, float hpRatio, float ratio)
    {
        var cost = WireLimits.Saturate(maxHp * (double)hpRatio);
        var gain = WireLimits.Saturate(maxHp * (double)hpRatio * ratio);
        var newHp = hp - cost;
        if (newHp <= 0) return new ManaConversion(false, hp, mp);
        return new ManaConversion(true, newHp, Math.Min(mp + gain, maxMp));
    }

    public static IReadOnlyList<int> CleansedBy(SkillTableRow action, int entries)
    {
        ArgumentNullException.ThrowIfNull(action);
        var cleansed = new List<int>(entries);
        for (var entry = 0; entry < entries; entry++)
        {
            if (action.Amount(CleansedColumnPrefix + entry) is { } actionId && actionId > NoAction) cleansed.Add(actionId);
        }
        return cleansed;
    }
}
