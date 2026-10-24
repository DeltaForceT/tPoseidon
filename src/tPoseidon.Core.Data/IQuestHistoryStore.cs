namespace tPoseidon.Core.Data;

public interface IQuestHistoryStore
{
        QuestHistory? Find(ulong characterId, byte questType, int questId, int monsterId);

        void Insert(QuestHistory row);

        bool Update(QuestHistory row);

        IReadOnlyList<QuestHistory> GetForCharacter(ulong characterId);

        void AppendLog(QuestHistory row);

        bool CompletedToday(ulong characterId, byte questType, int questId, int monsterId, DateTime referenceUtc);

        bool HasCompletedAtLeast(ulong characterId, byte questType, int questId, int monsterId, byte threshold);
}
