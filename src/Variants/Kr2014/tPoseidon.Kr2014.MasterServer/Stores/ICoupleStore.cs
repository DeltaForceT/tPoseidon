namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface ICoupleStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> ProposeAsync(ulong charUid, ulong partnerCharUid, CancellationToken ct);
    ValueTask<bool> AcceptAsync(ulong charUid, ulong partnerCharUid, CancellationToken ct);
    ValueTask<bool> BreakUpAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> UpdateAffinityAsync(ulong charUid, int delta, CancellationToken ct);
}
