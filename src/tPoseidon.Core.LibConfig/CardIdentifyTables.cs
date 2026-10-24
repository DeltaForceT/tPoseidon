namespace tPoseidon.Core.LibConfig;

public sealed record CardIdentifyGradeEntry(int Grade, int Table0, float Rate0, int Table1, float Rate1, int SpecialTable, float SpecialRate);

public readonly record struct CardIdentifyResultEntry(int ItemId, int Rate);

public sealed record CardIdentifyTables(
    IReadOnlyDictionary<int, CardIdentifyGradeEntry> Grades,
    IReadOnlyDictionary<int, IReadOnlyList<CardIdentifyResultEntry>> Results,
    IReadOnlyList<int> ComboRatios,
    float ExpRatio,
    float TmExpRatio)
{
    public const string GradeTable = "CardIdentify_GradeInfo";
    public const string ResultTablePrefix = "CardIdentify_ResultTable";
    public const string ComboTable = "CardIdentify_ComboTable";
    public const string ExpTable = "CardIdentify_Exp";

    public static CardIdentifyTables Empty { get; } = new(
        new Dictionary<int, CardIdentifyGradeEntry>(),
        new Dictionary<int, IReadOnlyList<CardIdentifyResultEntry>>(),
        Array.Empty<int>(),
        0f,
        0f);
}
