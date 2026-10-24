namespace tPoseidon.Core.LibConfig;

public readonly record struct PresentDrop(int ItemId, int Rate, int Count);

public sealed record PresentItemEntry(int BoxItemId, int Type, int DropCount, IReadOnlyList<PresentDrop> Drops)
{
    public const string TableName = "PresentItemParam2";
    public const int DropColumns = 20;
}
