namespace tPoseidon.Core.LibConfig;

public sealed record CompoundRecipeEntry
{
    public required string TableName { get; init; }
    public required int CompoundId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int ResLevel { get; init; }
    public IReadOnlyList<int> ResIds { get; init; } = Array.Empty<int>();
    public IReadOnlyList<(int ItemId, int Count)> Requirements { get; init; } = Array.Empty<(int, int)>();
    public int Probability { get; init; }
    public int Fee { get; init; }
    public int WasteItem { get; init; }
}

public sealed record CompounderShopEntry
{
    public required string MapName { get; init; }
    public required int CompoundId { get; init; }
    public int Probability { get; init; }
    public int Hidden { get; init; }
}
