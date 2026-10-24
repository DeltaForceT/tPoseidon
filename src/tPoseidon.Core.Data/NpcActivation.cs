namespace tPoseidon.Core.Data;

public sealed class NpcActivation
{
    private readonly object _gate = new();
    private readonly HashSet<ushort> _inactive = new();

    public bool IsActive(ushort entityId)
    {
        lock (_gate) return !_inactive.Contains(entityId);
    }

    public bool Set(ushort entityId, bool active)
    {
        lock (_gate) return active ? _inactive.Remove(entityId) : _inactive.Add(entityId);
    }

    public IReadOnlyList<ushort> Inactive()
    {
        lock (_gate) return _inactive.OrderBy(id => id).ToArray();
    }
}
