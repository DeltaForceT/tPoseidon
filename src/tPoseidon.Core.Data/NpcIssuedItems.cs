namespace tPoseidon.Core.Data;

public readonly record struct NpcIssuedItem(uint ItemId, ushort Count);

public sealed class NpcIssuedItems
{
    private readonly object _gate = new();
    private readonly Dictionary<ushort, NpcIssuedItem> _issued = new();

    public void Set(ushort npcEntityId, uint itemId, ushort count)
    {
        lock (_gate) _issued[npcEntityId] = new NpcIssuedItem(itemId, count);
    }

    public NpcIssuedItem? Get(ushort npcEntityId)
    {
        lock (_gate) return _issued.TryGetValue(npcEntityId, out var issued) ? issued : null;
    }

    public bool TryTakeOne(ushort npcEntityId, uint itemId)
    {
        lock (_gate)
        {
            if (!_issued.TryGetValue(npcEntityId, out var issued) || issued.ItemId != itemId || issued.Count == 0) return false;
            _issued[npcEntityId] = issued with { Count = (ushort)(issued.Count - 1) };
            return true;
        }
    }
}

public static class NpcIssueRules
{
    public const int Taken = 1;
    public const int NoneLeft = 2;
    public const int NoIssuedItem = 3;
    public const int NoRoom = 4;
    public const int AlreadyHeld = 5;
}
