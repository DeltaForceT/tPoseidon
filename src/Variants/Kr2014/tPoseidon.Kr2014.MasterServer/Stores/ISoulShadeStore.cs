namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface ISoulShadeStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> InsertAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UpdateAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> DeleteAsync(ulong charUid, ulong soulShadeUid, CancellationToken ct);
}
