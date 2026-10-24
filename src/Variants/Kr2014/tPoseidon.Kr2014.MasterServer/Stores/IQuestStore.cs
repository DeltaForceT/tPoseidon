namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IQuestStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadHistoryAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> AcceptQuestAsync(ulong charUid, ushort questId, byte questType, CancellationToken ct);
    ValueTask<bool> CompleteQuestAsync(ulong charUid, ushort questId, byte questType, ushort skillId, CancellationToken ct);
    ValueTask<bool> AbandonQuestAsync(ulong charUid, ushort questId, byte questType, CancellationToken ct);
    ValueTask<bool> UpdateProgressAsync(ulong charUid, ushort questId, int monsterId, short killCount, CancellationToken ct);
}
