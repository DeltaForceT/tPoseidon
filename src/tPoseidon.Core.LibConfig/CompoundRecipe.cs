namespace tPoseidon.Core.LibConfig;

public sealed record CompoundRequirement(int ItemId, int Count);

public sealed record CompoundRecipe
{
    public const int PotionType = 0;
    public const int RareType = 1;
    public const int ThrowType = 2;
    public const int ResultColumns = 3;
    public const int RequirementColumns = 5;

    public required int Id { get; init; }
    public required int Type { get; init; }
    public IReadOnlyList<int> Results { get; init; } = Array.Empty<int>();
    public IReadOnlyList<CompoundRequirement> Requirements { get; init; } = Array.Empty<CompoundRequirement>();
    public int Probability { get; init; }
    public int Fee { get; init; }
    public int WasteItem { get; init; }
}

public sealed record CompounderEntry(int RecipeId, bool Hidden);

public sealed record CompounderList
{
    public const int MaxLists = 64;

    public required string Name { get; init; }
    public int Grade { get; init; }
    public IReadOnlyList<CompounderEntry> Entries { get; init; } = Array.Empty<CompounderEntry>();
}
