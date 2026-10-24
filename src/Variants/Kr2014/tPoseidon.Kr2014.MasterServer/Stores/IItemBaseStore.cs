namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IItemBaseStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> InsertAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UpdateAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> DeleteAsync(ulong charUid, ulong itemUid, CancellationToken ct);
    ValueTask<bool> MoveAsync(ulong charUid, ulong itemUid, byte fromSlot, byte toSlot, CancellationToken ct);
    ValueTask<bool> EquipAsync(ulong charUid, ulong itemUid, byte slot, bool release, CancellationToken ct);
}
