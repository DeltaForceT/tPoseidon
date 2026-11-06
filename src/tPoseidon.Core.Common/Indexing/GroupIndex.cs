namespace tPoseidon.Core.Common.Indexing;

public sealed class GroupIndex<TKey, TRow>
    where TKey : notnull
{
    private static readonly TRow[] None = Array.Empty<TRow>();

    private readonly Dictionary<TKey, TRow[]> _groups;

    public GroupIndex(IEnumerable<TRow> rows, Func<TRow, TKey> keyOf)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(keyOf);
        var lists = new Dictionary<TKey, List<TRow>>();
        foreach (var row in rows)
        {
            var key = keyOf(row);
            if (!lists.TryGetValue(key, out var list)) lists[key] = list = new List<TRow>();
            list.Add(row);
        }
        _groups = lists.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }

    public IReadOnlyList<TRow> Of(TKey key) => _groups.TryGetValue(key, out var rows) ? rows : None;
}
