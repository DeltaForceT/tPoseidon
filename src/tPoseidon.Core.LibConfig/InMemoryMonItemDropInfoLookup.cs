namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryMonItemDropInfoLookup : IMonItemDropInfoLookup
{
    private readonly Dictionary<int, MonItemDropInfoEntry> _byId;

    public InMemoryMonItemDropInfoLookup(IEnumerable<MonItemDropInfoEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _byId = new Dictionary<int, MonItemDropInfoEntry>();
        foreach (var r in rows) _byId[r.Id] = r;
    }

    public MonItemDropInfoEntry? GetById(int dropTableId) =>
        _byId.TryGetValue(dropTableId, out var r) ? r : null;

    public int Count => _byId.Count;
}
