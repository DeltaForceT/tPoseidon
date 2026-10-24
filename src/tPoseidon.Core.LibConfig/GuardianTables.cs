namespace tPoseidon.Core.LibConfig;

public sealed record GuardianParamEntry(int Id, int FType, int LevelGrowthId, IReadOnlyList<int> RuneStones, IReadOnlyList<int> DefaultRuneSkills);

public sealed record GuardianGrowthEntry(int Id, IReadOnlyList<float> Ratios);

public sealed record GuardianBasicStatEntry(int Level, IReadOnlyList<int> Points);

public sealed record GuardianExpRatioEntry(int ExpType, float Ratio);

public sealed record GuardianTables(
    IReadOnlyDictionary<int, int> NextExpByLevel,
    IReadOnlyDictionary<int, GuardianParamEntry> Params,
    IReadOnlyDictionary<int, GuardianGrowthEntry> Growth,
    IReadOnlyDictionary<int, GuardianBasicStatEntry> BasicStats,
    IReadOnlyDictionary<int, float> ExpRatios)
{
    public const int RuneSlotCount = 4;

    public static readonly string[] GrowthColumns =
        ["Ratio_Ap", "Ratio_Ac", "Ratio_Dx", "Ratio_MaxMp", "Ratio_Ma", "Ratio_Md", "Ratio_MaxWt", "Ratio_Da", "Ratio_Lk", "Ratio_MaxHp", "Ratio_Dp", "Ratio_Hv"];

    public static readonly string[] BasicStatColumns = ["AP", "AC", "DX", "MP", "MA", "MD", "WT", "DA", "LK", "HP", "DP", "HV"];
}
