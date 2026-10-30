namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryRandomResultLookup : IRandomResultTables
{
    private readonly Dictionary<int, RandomResultTable> _byId;

    public InMemoryRandomResultLookup(IEnumerable<RandomResultTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        _byId = new Dictionary<int, RandomResultTable>();
        foreach (var table in tables) _byId[table.Id] = table;
    }

    public static InMemoryRandomResultLookup Empty { get; } = new(Array.Empty<RandomResultTable>());

    public int Count => _byId.Count;

    public RandomResultTable? GetById(int id) => _byId.TryGetValue(id, out var table) ? table : null;
}
