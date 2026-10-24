namespace tPoseidon.Core.Data;

public sealed record GuardianSoulRow(ulong GuardianUid, int Gauge, ulong SourceItemUid, int SourceCount, int PotionsReady, DateTimeOffset LastTick);

public sealed record SoulAccount(uint Soul, IReadOnlyList<GuardianSoulRow> Guardians);

public interface ISoulStore
{
    SoulAccount Load(ulong characterId);

    void SaveSoul(ulong characterId, uint soul);

    bool SaveGuardian(ulong characterId, GuardianSoulRow row);
}
