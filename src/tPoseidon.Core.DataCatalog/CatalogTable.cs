namespace tPoseidon.Core.DataCatalog;

public sealed class CatalogTable<TKey, TRow>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TRow> _byKey;

    public CatalogTable(string name, IReadOnlyList<TRow> rows, Func<TRow, TKey> keyOf)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keyOf);
        Name = name;
        Rows = rows;
        _byKey = new Dictionary<TKey, TRow>(rows.Count);
        foreach (var row in rows) _byKey[keyOf(row)] = row;
    }

    public string Name { get; }

    public IReadOnlyList<TRow> Rows { get; }

    public int Count => _byKey.Count;

    public TRow? Find(TKey key) => _byKey.TryGetValue(key, out var row) ? row : default;

    public bool TryGet(TKey key, out TRow row)
    {
        if (_byKey.TryGetValue(key, out var found))
        {
            row = found;
            return true;
        }
        row = default!;
        return false;
    }
}
