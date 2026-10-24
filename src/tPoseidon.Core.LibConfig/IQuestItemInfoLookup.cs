namespace tPoseidon.Core.LibConfig;

public interface IQuestItemInfoLookup
{
        IReadOnlyList<QuestItemInfoEntry> GetByQuestId(int questId);

        QuestItemInfoEntry? GetByItemId(int itemId);

        int Count { get; }
}
