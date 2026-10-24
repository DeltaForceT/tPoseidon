namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface ICharStore
{
    ValueTask<bool> MakeNewCharacterAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> InsertIntoDbAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> DeleteFromDbAsync(ulong charUid, CancellationToken ct);
    ValueTask<ReadOnlyMemory<byte>> LoadChrInfoAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> LogoutAsync(ulong charUid, CancellationToken ct);

    ValueTask<bool> UpdateStateAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UpdateStateMoneyAsync(ulong charUid, long delta, CancellationToken ct);
    ValueTask<bool> UpdateStateExpAsync(ulong charUid, long delta, CancellationToken ct);
    ValueTask<bool> UpdateStateHairColorAsync(ulong charUid, byte hairType, byte hairColor, CancellationToken ct);
    ValueTask<bool> UpdateStatusAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UpdateTutorialAsync(ulong charUid, uint tutorialStep, CancellationToken ct);
    ValueTask<bool> BlockAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UnblockAsync(ulong charUid, CancellationToken ct);

    ValueTask<bool> ResetStatusAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> ResetGrowthGraphAsync(ulong charUid, CancellationToken ct);

    ValueTask<ReadOnlyMemory<byte>> LoadBossKillInfoAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> SaveBossKillInfoAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);

    ValueTask<bool> ExchangeMoneyAsync(ulong charUid, long delta, CancellationToken ct);
    ValueTask<bool> AddWarehouseSlotAsync(ulong charUid, byte slots, CancellationToken ct);

    ValueTask<bool> UpdateCJQuestStateAsync(ulong charUid, byte state, CancellationToken ct);
    ValueTask<bool> UpdateOccupationAsync(ulong charUid, ushort occupation, CancellationToken ct);
    ValueTask<bool> UpdateOccupationIndexAsync(ulong charUid, ushort occupationIndex, CancellationToken ct);
    ValueTask<bool> UpdateChrTypeAsync(ulong charUid, byte chrType, CancellationToken ct);
    ValueTask<bool> UpdateDiggingCountAsync(ulong charUid, int count, CancellationToken ct);
    ValueTask<bool> GiveMcItemAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> ChangeCharNameAsync(ulong charUid, string newName, CancellationToken ct);
    ValueTask<bool> AddBroadcastMessageAsync(ulong charUid, string message, CancellationToken ct);
    ValueTask<bool> OpenMemoryPortAsync(ulong charUid, ushort zoneId, CancellationToken ct);
    ValueTask<bool> UseMemoryPortAsync(ulong charUid, ushort zoneId, CancellationToken ct);
    ValueTask<bool> AddMemoryPortAsync(ulong charUid, ushort zoneId, CancellationToken ct);
    ValueTask<bool> DeleteMemoryPortAsync(ulong charUid, ushort zoneId, CancellationToken ct);
    ValueTask<bool> UpdateCutReasonAsync(ulong charUid, byte reason, CancellationToken ct);
    ValueTask<bool> OutTimeCapsuleAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
    ValueTask<bool> UpdatePKInfoAsync(ulong charUid, ReadOnlyMemory<byte> body, CancellationToken ct);
}
