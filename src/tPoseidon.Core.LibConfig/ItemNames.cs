namespace tPoseidon.Core.LibConfig;

public sealed record ItemName(int Id, string Name, string Table, ItemParamEntry Definition);

public sealed class ItemNames
{
    public const string ItemTable = "ItemParam2";
    public const string ExtendedTable = "ItemParamEx";
    public const string Extended2Table = "ItemParamEx2";
    public const string CashTable = "ItemParamCM2";

    public static IReadOnlyList<string> Tables { get; } = new[] { ItemTable, ExtendedTable, Extended2Table, CashTable };

    private readonly Dictionary<int, ItemName> _names;

    public ItemNames(IEnumerable<(string Table, ItemParamEntry Entry)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var names = new Dictionary<int, ItemName>();
        foreach (var (table, entry) in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Name)) names.TryAdd(entry.Id, new ItemName(entry.Id, entry.Name.Trim(), table, entry));
        }
        _names = names;
    }

    public int Count => _names.Count;

    public IReadOnlyCollection<ItemName> All => _names.Values;

    public string? Of(int itemIndex) => _names.TryGetValue(itemIndex, out var name) ? name.Name : null;

    public static ItemNames Load(string libconfigPath)
    {
        var reader = new LibConfigXmlReader(libconfigPath);
        return new ItemNames(
            reader.LoadItemParam2().Select(entry => (ItemTable, entry))
                .Concat(reader.LoadItemParamExtended().Select(entry => (ExtendedTable, entry)))
                .Concat(reader.LoadItemParamEx2().Select(entry => (Extended2Table, entry)))
                .Concat(reader.LoadItemParamCm2().Select(entry => (CashTable, entry))));
    }
}
