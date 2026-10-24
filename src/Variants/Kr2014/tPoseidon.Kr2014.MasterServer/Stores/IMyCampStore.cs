namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IMyCampStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> CreateAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> JoinAsync(ulong campId, ulong charUid, CancellationToken ct);
    ValueTask<bool> LeaveAsync(ulong campId, ulong charUid, CancellationToken ct);
    ValueTask<bool> DisbandAsync(ulong campId, CancellationToken ct);
    ValueTask<bool> UpdateAsync(ulong campId, ReadOnlyMemory<byte> body, CancellationToken ct);
}
