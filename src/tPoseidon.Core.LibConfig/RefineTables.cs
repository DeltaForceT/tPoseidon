namespace tPoseidon.Core.LibConfig;

public sealed record RefineLevelRow(int RefineIndex, IReadOnlyList<int> Accumulation, IReadOnlyList<float> Ratio);

public sealed record RefineMaterialRow(int MinLevel, int MaxLevel, IReadOnlyList<int> ItemIds, IReadOnlyList<int> ItemCounts);

public sealed record RefineSupportRow(int Id, int MinRefineLevel, int MaxRefineLevel, int ItemId, int ItemCount, float Prob);

public sealed record RefineTables
{
    public const int Levels = 13;
    public const int Types = 7;

    public IReadOnlyDictionary<int, RefineLevelRow> LevelRows { get; init; } = new Dictionary<int, RefineLevelRow>();
    public IReadOnlyList<IReadOnlyList<RefineMaterialRow>> Materials { get; init; } = Array.Empty<IReadOnlyList<RefineMaterialRow>>();
    public IReadOnlyDictionary<int, RefineSupportRow> Supports { get; init; } = new Dictionary<int, RefineSupportRow>();
    public float CostDiscount { get; init; }
    public float RefineSeed { get; init; }
}
