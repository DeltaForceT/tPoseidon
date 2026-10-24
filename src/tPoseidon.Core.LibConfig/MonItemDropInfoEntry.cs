namespace tPoseidon.Core.LibConfig;

public sealed record MonItemDropInfoEntry
{
    public required int Id { get; init; }
    public required IReadOnlyList<DropItem> Items { get; init; }
    public required IReadOnlyList<DropTableRef> SubTables { get; init; }
}

public readonly record struct DropItem(int ItemId, float Rate, int Count);
public readonly record struct DropTableRef(int TableId, float Rate);
