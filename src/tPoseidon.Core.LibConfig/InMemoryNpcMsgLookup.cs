namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryNpcMsgLookup : INpcMsgLookup
{
    private readonly Dictionary<(string Table, int Row), NpcMsgEntry> _byKey;
    private readonly Dictionary<string, int> _rowCountByTable;

    public InMemoryNpcMsgLookup(IEnumerable<NpcMsgEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byKey = new Dictionary<(string, int), NpcMsgEntry>();
        _rowCountByTable = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {



            _byKey[(entry.TableName, entry.No)] = entry;


            _rowCountByTable[entry.TableName] =
                _rowCountByTable.TryGetValue(entry.TableName, out var c) ? c + 1 : 1;
        }
    }

        public static InMemoryNpcMsgLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        var entries = reader.LoadAllNpcMsgTables();
        return new InMemoryNpcMsgLookup(entries);
    }

    public NpcMsgEntry? GetRow(string tableName, int rowNo)
    {
        return _byKey.TryGetValue((tableName, rowNo), out var entry) ? entry : null;
    }

    public int GetRowCount(string tableName)
        => _rowCountByTable.TryGetValue(tableName, out var c) ? c : 0;

    public int Count => _byKey.Count;
}
