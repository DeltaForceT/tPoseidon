namespace tPoseidon.Core.LibConfig;

public interface IQuestInfoLookup
{
        QuestInfoEntry? GetById(int questId);

        int Count { get; }
}
