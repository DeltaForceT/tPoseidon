using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Monsters;

public sealed record FieldItem(
    ushort EntityId,
    ushort MapId,
    ushort X,
    ushort Y,
    int ItemId,
    int Count,
    byte ItemClass,
    bool IsMoney,
    ushort DropperEntityId,
    ushort OwnerEntityId,
    DateTimeOffset DroppedAt)
{
    public const ushort NoOwner = 0;

    public bool IsHidden { get; init; }

    public ulong ItemUid { get; init; }
}

public sealed class GroundItemField
{
    public static readonly TimeSpan OwnedFor = TimeSpan.FromMilliseconds(30000);
    public static readonly TimeSpan FreeFor = TimeSpan.FromMilliseconds(90000);
    public static readonly TimeSpan MoneyFreeFor = TimeSpan.FromMilliseconds(60000);

    private readonly ConcurrentDictionary<ushort, FieldItem> _items = new();

    public void Add(FieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items[item.EntityId] = item;
    }

    public FieldItem? Find(ushort entityId) => _items.TryGetValue(entityId, out var item) ? item : null;

    public bool Remove(ushort entityId) => _items.TryRemove(entityId, out _);

    public IReadOnlyList<FieldItem> OnMap(ushort mapId)
    {
        var onMap = new List<FieldItem>();
        foreach (var item in _items.Values)
        {
            if (item.MapId == mapId) onMap.Add(item);
        }
        return onMap;
    }

    public static ushort OwnerAt(FieldItem item, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.OwnerEntityId != FieldItem.NoOwner && now - item.DroppedAt <= OwnedFor
            ? item.OwnerEntityId
            : FieldItem.NoOwner;
    }

    public static DateTimeOffset RemovedAt(FieldItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var free = item.IsMoney ? MoneyFreeFor : FreeFor;
        return item.OwnerEntityId == FieldItem.NoOwner
            ? item.DroppedAt + free
            : item.DroppedAt + OwnedFor + free;
    }

    public IReadOnlyList<FieldItem> TakeExpired(DateTimeOffset now)
    {
        List<FieldItem>? expired = null;
        foreach (var item in _items.Values)
        {
            if (now <= RemovedAt(item)) continue;
            if (_items.TryRemove(item.EntityId, out var removed)) (expired ??= new List<FieldItem>()).Add(removed);
        }
        return expired ?? (IReadOnlyList<FieldItem>)Array.Empty<FieldItem>();
    }
}
