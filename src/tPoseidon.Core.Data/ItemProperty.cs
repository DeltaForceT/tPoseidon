namespace tPoseidon.Core.Data;

public readonly record struct ItemProperty(int Type, int Id, short Value, uint Extra);

public static class ItemPropertyTypes
{
    public const int LastMixSlot = 9;
    public const int Stat = 10;
    public const int RefineBonus = 0xB;
    public const int Options = 0xD;
    public const int MaxPerItem = 20;
    public const int MaxStored = 19;
}

public interface IItemPropertyStore
{
    IReadOnlyList<ItemProperty> ForItem(ulong itemUid);

    void Replace(ulong itemUid, IReadOnlyList<ItemProperty> properties);

    void Delete(ulong itemUid);
}

public interface IItemRefineStore
{
    void SetRefine(ulong itemUid, byte level, byte state);

    void SetMixSlot(ulong itemUid, byte slots);
}

public sealed class InMemoryItemPropertyStore : IItemPropertyStore
{
    private readonly Dictionary<ulong, IReadOnlyList<ItemProperty>> _properties = new();
    private readonly object _lock = new();

    public IReadOnlyList<ItemProperty> ForItem(ulong itemUid)
    {
        lock (_lock) return _properties.TryGetValue(itemUid, out var list) ? list : Array.Empty<ItemProperty>();
    }

    public void Replace(ulong itemUid, IReadOnlyList<ItemProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        lock (_lock) _properties[itemUid] = properties.ToArray();
    }

    public void Delete(ulong itemUid)
    {
        lock (_lock) _properties.Remove(itemUid);
    }
}
