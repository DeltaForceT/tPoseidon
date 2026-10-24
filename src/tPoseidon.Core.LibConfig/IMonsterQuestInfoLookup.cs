namespace tPoseidon.Core.LibConfig;

public interface IMonsterQuestInfoLookup
{
        MonsterQuestInfoEntry? GetById(int questId);

        int Count { get; }

        IEnumerable<MonsterQuestInfoEntry> All();
}
