namespace tPoseidon.Core.DataCatalog;

public enum ItemTable
{
    Standard,
    Extended,
    Extended2,
    Cash,
}

public static class ItemTables
{
    public static IReadOnlyList<ItemTable> All { get; } = new[]
    {
        ItemTable.Standard,
        ItemTable.Extended,
        ItemTable.Extended2,
        ItemTable.Cash,
    };

    public static string NameOf(ItemTable table) => table switch
    {
        ItemTable.Standard => "ItemParam2",
        ItemTable.Extended => "ItemParamEx",
        ItemTable.Extended2 => "ItemParamEx2",
        ItemTable.Cash => "ItemParamCM2",
        _ => throw new ArgumentOutOfRangeException(nameof(table)),
    };
}
