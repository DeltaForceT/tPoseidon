namespace tPoseidon.Core.Data;

public interface IBankStore
{
    long GetMoney(uint accountId);

    long AdjustMoney(uint accountId, long delta);

    IReadOnlyList<Item> GetItems(uint accountId);

    Item Deposit(uint accountId, ulong itemUid, byte slotPosition);

    bool Withdraw(uint accountId, ulong itemUid);

    Item? FindItem(uint accountId, ulong itemUid);
}
