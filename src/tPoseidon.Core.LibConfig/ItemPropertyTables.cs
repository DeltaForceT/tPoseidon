namespace tPoseidon.Core.LibConfig;

public readonly record struct RandomStatRange(int Min, int Max, string Curve);

public sealed record RandomStatRow
{
    public const int StatCount = 13;

    public required int ItemId { get; init; }
    public IReadOnlyList<RandomStatRange?> Stats { get; init; } = Array.Empty<RandomStatRange?>();
    public RandomStatRange? MixSlot { get; init; }
}

public sealed record ItemPropertyTables
{
    public IReadOnlyDictionary<int, RandomStatRow> RandomStats { get; init; } = new Dictionary<int, RandomStatRow>();
    public IReadOnlyDictionary<string, IReadOnlyList<int>> Curves { get; init; } = new Dictionary<string, IReadOnlyList<int>>();
    public IReadOnlyDictionary<int, IReadOnlyList<(int Index, int Value)>> Attributes { get; init; } = new Dictionary<int, IReadOnlyList<(int Index, int Value)>>();
}
