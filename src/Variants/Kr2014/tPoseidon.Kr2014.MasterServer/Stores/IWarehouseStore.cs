namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IWarehouseStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong accountCode, CancellationToken ct);
    ValueTask<bool> DepositAsync(ulong accountCode, ulong charUid, ulong itemUid, byte slot, CancellationToken ct);
    ValueTask<bool> WithdrawAsync(ulong accountCode, ulong charUid, ulong itemUid, byte slot, CancellationToken ct);
    ValueTask<bool> AddSlotAsync(ulong accountCode, byte slots, CancellationToken ct);
    ValueTask<bool> ExchangeMoneyAsync(ulong accountCode, long delta, CancellationToken ct);
}
