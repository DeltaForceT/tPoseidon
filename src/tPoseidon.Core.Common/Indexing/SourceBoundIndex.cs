namespace tPoseidon.Core.Common.Indexing;

public sealed class SourceBoundIndex<TKey, TRow>
    where TKey : notnull
{
    private readonly Func<TRow, TKey> _keyOf;
    private volatile Bound? _bound;

    public SourceBoundIndex(Func<TRow, TKey> keyOf)
    {
        ArgumentNullException.ThrowIfNull(keyOf);
        _keyOf = keyOf;
    }

    public FirstIndex<TKey, TRow> Over(object identity, IEnumerable<TRow> rows)
    {
        var current = _bound;
        if (current is not null && ReferenceEquals(current.Identity, identity)) return current.Index;
        var built = new Bound(identity, new FirstIndex<TKey, TRow>(rows, _keyOf));
        _bound = built;
        return built.Index;
    }

    private sealed record Bound(object Identity, FirstIndex<TKey, TRow> Index);
}
