namespace tPoseidon.Core.Data;

public sealed record ShadowBonusRecord(ulong CharacterId, int RewardId, DateTimeOffset ExpiresAt);

public interface IShadowBonusStore
{
    ShadowBonusRecord? Load(ulong characterId);

    void Save(ShadowBonusRecord record);
}
