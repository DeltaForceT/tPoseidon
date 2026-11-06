using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog;

public sealed class ItemCatalog
{
    private readonly Dictionary<ItemTable, IReadOnlyList<ItemParamEntry>> _rows;
    private readonly Dictionary<ItemTable, Dictionary<int, ItemParamEntry>> _byTable;
    private readonly Lazy<IReadOnlyList<ItemClash>> _clashes;

    public ItemCatalog(IReadOnlyList<ItemParamEntry> itemParam2, IReadOnlyList<ItemParamEntry> itemParamEx, IReadOnlyList<ItemParamEntry> itemParamEx2, IReadOnlyList<ItemParamEntry> itemParamCm2)
    {
        ArgumentNullException.ThrowIfNull(itemParam2);
        ArgumentNullException.ThrowIfNull(itemParamEx);
        ArgumentNullException.ThrowIfNull(itemParamEx2);
        ArgumentNullException.ThrowIfNull(itemParamCm2);
        _rows = new Dictionary<ItemTable, IReadOnlyList<ItemParamEntry>>
        {
            [ItemTable.Standard] = itemParam2,
            [ItemTable.Extended] = itemParamEx,
            [ItemTable.Extended2] = itemParamEx2,
            [ItemTable.Cash] = itemParamCm2,
        };
        _byTable = _rows.ToDictionary(pair => pair.Key, pair => Index(pair.Value));
        _clashes = new Lazy<IReadOnlyList<ItemClash>>(FindClashes);
    }

    public static ItemCatalog Load(LibConfigXmlReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new ItemCatalog(reader.LoadItemParam2(), reader.LoadItemParamExtended(), reader.LoadItemParamEx2(), reader.LoadItemParamCm2());
    }

    public static ItemCatalog Load(FastLibConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return new ItemCatalog(reader.LoadItemParam2(), reader.LoadItemParamExtended(), reader.LoadItemParamEx2(), reader.LoadItemParamCm2());
    }

    public ItemNames ToItemNames() => new(ItemTables.All.SelectMany(table => Rows(table).Select(entry => (ItemTables.NameOf(table), entry))));

    public IEnumerable<ItemParamEntry> AllRows() => ItemTables.All.SelectMany(Rows);

    public IReadOnlyList<ItemParamEntry> Rows(ItemTable table) => _rows[table];

    public int Count(ItemTable table) => _byTable[table].Count;

    public ItemParamEntry? Find(ItemTable table, int id) => _byTable[table].GetValueOrDefault(id);

    public ItemRecord? Get(int id) => Get(id, ItemPrecedence.ServerDefinition);

    public ItemRecord? Get(int id, ItemPrecedence precedence)
    {
        ArgumentNullException.ThrowIfNull(precedence);
        foreach (var table in precedence.HighestFirst)
        {
            if (_byTable[table].TryGetValue(id, out var entry)) return new ItemRecord(table, entry);
        }
        return null;
    }

    public ItemRecord? GetNamed(int id)
    {
        foreach (var table in ItemPrecedence.DisplayName.HighestFirst)
        {
            if (_byTable[table].TryGetValue(id, out var entry) && !string.IsNullOrWhiteSpace(entry.Name)) return new ItemRecord(table, entry);
        }
        return null;
    }

    public IReadOnlyList<ItemTable> TablesHolding(int id) =>
        ItemTables.All.Where(table => _byTable[table].ContainsKey(id)).ToArray();

    public IReadOnlyList<ItemClash> Clashes => _clashes.Value;

    public int DistinctIds => ItemTables.All.SelectMany(table => _byTable[table].Keys).Distinct().Count();

    public IEnumerable<ItemRecord> Resolved(ItemPrecedence precedence)
    {
        ArgumentNullException.ThrowIfNull(precedence);
        var seen = new HashSet<int>();
        foreach (var table in precedence.HighestFirst)
        {
            foreach (var pair in _byTable[table])
            {
                if (seen.Add(pair.Key)) yield return new ItemRecord(table, pair.Value);
            }
        }
    }

    private static Dictionary<int, ItemParamEntry> Index(IReadOnlyList<ItemParamEntry> rows)
    {
        var byId = new Dictionary<int, ItemParamEntry>(rows.Count);
        foreach (var row in rows) byId[row.Id] = row;
        return byId;
    }

    private ItemClash[] FindClashes()
    {
        var holders = new Dictionary<int, List<ItemTable>>();
        foreach (var table in ItemTables.All)
        {
            foreach (var id in _byTable[table].Keys)
            {
                if (!holders.TryGetValue(id, out var list)) holders[id] = list = new List<ItemTable>(2);
                list.Add(table);
            }
        }
        return holders.Where(pair => pair.Value.Count > 1)
            .OrderBy(pair => pair.Key)
            .Select(pair => new ItemClash(pair.Key, pair.Value))
            .ToArray();
    }
}
