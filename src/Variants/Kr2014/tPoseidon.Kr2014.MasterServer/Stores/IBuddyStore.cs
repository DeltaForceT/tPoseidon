namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IBuddyStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> AddAsync(ulong charUid, ulong buddyCharUid, CancellationToken ct);
    ValueTask<bool> RemoveAsync(ulong charUid, ulong buddyCharUid, CancellationToken ct);
    ValueTask<bool> BlockAsync(ulong charUid, ulong buddyCharUid, CancellationToken ct);
    ValueTask<bool> UnblockAsync(ulong charUid, ulong buddyCharUid, CancellationToken ct);
}
