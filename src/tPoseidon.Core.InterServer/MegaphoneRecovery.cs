using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MegaphoneRecovery
{
    public const int InventoryCapacity = 300;

    private readonly IMegaphoneQueueStore _queue;
    private readonly IItemStore _items;
    private readonly Action<string> _log;

    public MegaphoneRecovery(IMegaphoneQueueStore queue, IItemStore items, Action<string> log)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public int Run()
    {
        var interrupted = _queue.Interrupted();
        foreach (var shout in interrupted)
        {
            var flag = Refund(shout) ? MegaphoneFlags.InterruptedRefunded : MegaphoneFlags.InterruptedBagFull;
            _queue.Settle(shout.Code, flag);
            _log($"megaphone: {shout.Code} of 0x{shout.CharacterId:X} was interrupted by the restart, flag {flag}");
        }
        return interrupted.Count;
    }

    private bool Refund(QueuedMegaphone shout)
    {
        if (shout.Refund.ItemIndex <= 0) return true;
        var bag = _items.GetForCharacterBySlotKind(shout.CharacterId, SlotKind.Inventory);
        if (bag.FirstOrDefault(held => held.ItemIndex == shout.Refund.ItemIndex) is { } held)
        {
            _items.AdjustStack(held.ItemUid, 1);
            return true;
        }
        if (bag.Count >= InventoryCapacity) return false;
        var taken = bag.Select(item => item.SlotPosition).ToHashSet();
        var position = Enumerable.Range(0, InventoryCapacity).First(slot => !taken.Contains((byte)slot));
        _items.GrantToCharacter(shout.CharacterId, shout.Refund.ItemIndex, shout.Refund.ItemClass, SlotKind.Inventory, (byte)position);
        return true;
    }
}
