namespace tPoseidon.Core.DataCatalog;

public sealed class ItemPrecedence
{
    public static ItemPrecedence ServerDefinition { get; } = new(
        "ServerDefinition",
        ItemTable.Cash,
        ItemTable.Standard,
        ItemTable.Extended2,
        ItemTable.Extended);

    public static ItemPrecedence DisplayName { get; } = new(
        "DisplayName",
        ItemTable.Standard,
        ItemTable.Extended,
        ItemTable.Extended2,
        ItemTable.Cash);

    private ItemPrecedence(string name, params ItemTable[] highestFirst)
    {
        Name = name;
        HighestFirst = highestFirst;
    }

    public string Name { get; }

    public IReadOnlyList<ItemTable> HighestFirst { get; }
}
