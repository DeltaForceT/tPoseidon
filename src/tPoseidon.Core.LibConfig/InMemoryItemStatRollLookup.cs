namespace tPoseidon.Core.LibConfig;

public sealed class InMemoryItemStatRollLookup : IItemStatRollLookup
{
    private readonly IReadOnlyList<RandomStatValueEntry> _all;
    private readonly Dictionary<int, List<RandomStatValueEntry>> _byId;

    public InMemoryItemStatRollLookup(IEnumerable<RandomStatValueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var list = entries.ToList();
        _all = list;
        _byId = new Dictionary<int, List<RandomStatValueEntry>>();
        foreach (var e in list)
        {
            if (!_byId.TryGetValue(e.Id, out var bucket))
            {
                bucket = new List<RandomStatValueEntry>();
                _byId[e.Id] = bucket;
            }
            bucket.Add(e);
        }
    }

    public IReadOnlyCollection<RandomStatValueEntry> All => _all;
    public int Count => _all.Count;

    public IReadOnlyList<RandomStatValueEntry> GetById(int id)
        => _byId.TryGetValue(id, out var v) ? v : (IReadOnlyList<RandomStatValueEntry>)Array.Empty<RandomStatValueEntry>();
}
