using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class ScrollSkills
{
    public const int RainOfHeart = 1334;
    public const int StatUpScroll = 1387;
    public const int StatUpScrollDo = 1388;
    public const int WideHealing = 1424;
    public const int PartyDetector = 1425;

    public const string CalculateTypeColumn = "CalculateType";
    public const string PartySkillTypeColumn = "PartySkillType";
    public const string RadiusColumn = "Radius";
    public const string RatioColumn = "Ratio";
    public const string ValueColumn = "Value";
    public const string SecondsColumn = "Seconds";

    public static readonly IReadOnlyList<string> UpRatioColumns = new[]
    {
        "ApUpRatio", "AcUpRatio", "DxUpRatio", "MpUpRatio", "MaUpRatio", "MdUpRatio",
        "WtUpRatio", "DaUpRatio", "LkUpRatio", "HpUpRatio", "DpUpRatio", "HvUpRatio",
    };

    public static readonly IReadOnlyList<string> StringColumns = new[] { "BeginEffect", "ProcessEffect", "BeginSound", "ProcessSound" };

    public const int PartyScroll = 2;
    public const int FlatCalculation = 1;
    public const int LevelCalculation = 2;
    public const int PartyMembers = 6;
    public const int TextCodePage = 949;

    private const int LevelMultiplier = 4;
    private const int LevelDivisor = 10;
    private const int SmallStatDivisor = 40;

    private static readonly HashSet<int> LevelScaledStats = new()
    {
        SkillEffectKinds.StatAp, SkillEffectKinds.StatMp, SkillEffectKinds.StatMd,
        SkillEffectKinds.StatWt, SkillEffectKinds.StatHp, SkillEffectKinds.StatDp,
    };

    public static bool IsSelf(int actionId) => actionId == WideHealing;

    public static bool IsGround(int actionId) => actionId == PartyDetector;

    public static IReadOnlyList<(int Stat, int Delta)> StatDeltas(SkillTableRow row, int level)
    {
        ArgumentNullException.ThrowIfNull(row);
        var calculation = row.Amount(CalculateTypeColumn) ?? 0;
        var deltas = new List<(int Stat, int Delta)>();
        for (var stat = 0; stat < UpRatioColumns.Count; stat++)
        {
            var ratio = row.Value(UpRatioColumns[stat]) ?? 0f;
            if (ratio <= 0f) continue;
            var whole = (int)ratio;
            var delta = calculation switch
            {
                FlatCalculation => whole,
                LevelCalculation when stat == SkillEffectKinds.StatDx => 0,
                LevelCalculation when LevelScaledStats.Contains(stat) => whole * level * LevelMultiplier / LevelDivisor,
                LevelCalculation => whole * level / SmallStatDivisor,
                _ => 0,
            };
            if (delta > 0) deltas.Add((stat, delta));
        }
        return deltas;
    }

    public static uint Healed(uint hp, uint maxHp, float ratio) => (uint)Math.Min(maxHp, (long)(int)(maxHp * (double)ratio) + hp);
}
