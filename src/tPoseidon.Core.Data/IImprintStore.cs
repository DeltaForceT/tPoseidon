namespace tPoseidon.Core.Data;

public sealed record ImprintRecord(ulong CharacterId, int Type, int ItemId, DateTimeOffset ExpiresAt);

public interface IImprintStore
{
    IReadOnlyList<ImprintRecord> Load(ulong characterId);

    void Save(ImprintRecord record);
}
