namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IFortuneStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> DrawAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> UpdateAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
}
