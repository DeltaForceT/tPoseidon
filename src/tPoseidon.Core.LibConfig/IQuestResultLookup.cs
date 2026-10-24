namespace tPoseidon.Core.LibConfig;

public interface IQuestResultLookup
{
    QuestResultEntry? GetByQuestId(int questId, int count = 1);

    int Count { get; }
}
