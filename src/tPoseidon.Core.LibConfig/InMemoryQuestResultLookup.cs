namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryQuestResultLookup : IQuestResultLookup, IQuestResultTables
{
    private readonly Dictionary<(int QuestId, int Count), QuestResultEntry> _byKey;
    private readonly Dictionary<string, List<QuestResultEntry>> _byTable;

    public InMemoryQuestResultLookup(IEnumerable<QuestResultEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byKey = new Dictionary<(int, int), QuestResultEntry>();
        _byTable = new Dictionary<string, List<QuestResultEntry>>(StringComparer.Ordinal);
        foreach (var e in entries)
        {
            if (e.QuestId != 0) _byKey[(e.QuestId, e.Count)] = e;
            if (e.TableName.Length == 0) continue;
            if (!_byTable.TryGetValue(e.TableName, out var rows))
            {
                rows = new List<QuestResultEntry>();
                _byTable[e.TableName] = rows;
            }
            rows.Add(e);
        }
    }

    public static InMemoryQuestResultLookup LoadFromXml(string xmlPath)
    {
        var reader = new LibConfigXmlReader(xmlPath);
        var entries = reader.LoadAllQuestResultTables();
        return new InMemoryQuestResultLookup(entries);
    }

    public QuestResultEntry? GetByQuestId(int questId, int count = 1) =>
        _byKey.TryGetValue((questId, count), out var entry) ? entry : null;

    public IReadOnlyList<QuestResultEntry> RowsOf(string tableName) =>
        _byTable.TryGetValue(tableName, out var rows) ? rows : Array.Empty<QuestResultEntry>();

    public int Count => _byKey.Count;
}
