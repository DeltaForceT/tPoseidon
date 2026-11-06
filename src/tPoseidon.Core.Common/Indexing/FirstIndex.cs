namespace tPoseidon.Core.Common.Indexing;

public sealed class FirstIndex<TKey, TRow>
    where TKey : notnull
{
    private readonly Dictionary<TKey, TRow> _first;

    public FirstIndex(IEnumerable<TRow> rows, Func<TRow, TKey> keyOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keyOf);
        _first = new Dictionary<TKey, TRow>();
        foreach (var row in rows) _first.TryAdd(keyOf(row), row);
    }

    public int Count => _first.Count;

    public bool TryGet(TKey key, out TRow row)
    {
        if (_first.TryGetValue(key, out var found))
        {
            row = found;
            return true;
        }
        row = default!;
        return false;
    }

    public TRow? Find(TKey key) => _first.TryGetValue(key, out var row) ? row : default;
}
