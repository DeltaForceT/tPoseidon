namespace tPoseidon.Core.LibConfig;

public interface IMonsterParamLookup
{
    MonsterParamEntry? GetById(int monsterId);
    IReadOnlyCollection<MonsterParamEntry> All { get; }
    int Count { get; }
}
