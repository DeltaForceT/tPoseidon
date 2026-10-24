namespace tPoseidon.Kr2014.MasterServer.Stores;

public interface IGuildStore
{
    ValueTask<ReadOnlyMemory<byte>> LoadAsync(ulong charUid, CancellationToken ct);
    ValueTask<bool> CreateAsync(ulong leaderCharUid, string guildName, CancellationToken ct);
    ValueTask<bool> DisbandAsync(uint guildId, CancellationToken ct);
    ValueTask<bool> JoinAsync(uint guildId, ulong charUid, CancellationToken ct);
    ValueTask<bool> LeaveAsync(uint guildId, ulong charUid, CancellationToken ct);
    ValueTask<bool> PromoteAsync(uint guildId, ulong charUid, byte rank, CancellationToken ct);
    ValueTask<bool> KickAsync(uint guildId, ulong charUid, CancellationToken ct);
    ValueTask<bool> DepositMoneyAsync(uint guildId, ulong charUid, long amount, CancellationToken ct);
    ValueTask<bool> WithdrawMoneyAsync(uint guildId, ulong charUid, long amount, CancellationToken ct);
}
