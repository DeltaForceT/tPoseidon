namespace tPoseidon.Core.LibConfig;

public sealed record UniqueItemCountEntry
{
    public required int MinLevel { get; init; }
    public required int MaxLevel { get; init; }
    public required float Count { get; init; }
}

public sealed record LegendItemListEntry
{
    public required int ItemId { get; init; }
}

public sealed record LegendItemGroupEntry(int MonsterId, IReadOnlyList<(int ItemId, float Ratio)> Items)
{
    public const string Table = "LegendItemGroup";
    public const int Slots = 10;
}
