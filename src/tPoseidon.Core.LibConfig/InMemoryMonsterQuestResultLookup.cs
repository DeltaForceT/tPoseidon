namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonsterQuestResultLookup : IMonsterQuestResultTables
{
    private readonly Dictionary<string, MonsterQuestResultEntry?[]> _byTable;

    public InMemoryMonsterQuestResultLookup(IEnumerable<MonsterQuestResultEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _byTable = new Dictionary<string, MonsterQuestResultEntry?[]>(StringComparer.Ordinal);
        foreach (var table in entries.Where(e => e.Grade >= 0).GroupBy(e => e.TableName, StringComparer.Ordinal))
        {
            var rows = new MonsterQuestResultEntry?[table.Max(e => e.Grade) + 1];
            foreach (var row in table) rows[row.Grade] = row;
            _byTable[table.Key] = rows;
        }
    }

    public IReadOnlyList<MonsterQuestResultEntry?> RowsOf(string tableName) =>
        _byTable.TryGetValue(tableName, out var rows) ? rows : Array.Empty<MonsterQuestResultEntry?>();

    public int Count => _byTable.Count;
}
