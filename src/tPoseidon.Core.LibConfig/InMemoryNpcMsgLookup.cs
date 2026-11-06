namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryNpcMsgLookup : INpcMsgLookup
{
    private readonly Dictionary<(string Table, int Row), NpcMsgEntry> _byKey;
    private readonly Dictionary<string, int> _rowCountByTable;

    public InMemoryNpcMsgLookup(IEnumerable<NpcMsgEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byKey = new Dictionary<(string, int), NpcMsgEntry>();
        _rowCountByTable = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            _byKey[(KeyOf(entry.TableName), entry.No)] = entry;

            _rowCountByTable[entry.TableName] =
                _rowCountByTable.TryGetValue(entry.TableName, out var c) ? c + 1 : 1;
        }
    }


    public NpcMsgEntry? GetRow(string tableName, int rowNo)
    {
        return _byKey.TryGetValue((KeyOf(tableName), rowNo), out var entry) ? entry : null;
    }

    public int GetRowCount(string tableName)
        => _rowCountByTable.TryGetValue(tableName, out var c) ? c : 0;

    public int Count => _byKey.Count;

    private static string KeyOf(string tableName) => tableName.ToUpperInvariant();
}
