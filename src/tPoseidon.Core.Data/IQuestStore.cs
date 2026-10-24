namespace tPoseidon.Core.Data;

public interface IQuestStore
{
        void AddActive(Quest quest);

        IReadOnlyList<Quest> GetActiveForCharacter(ulong characterId);

        Quest? FindActive(ulong characterId, byte questType, int questId, int monsterId);

        int CountActiveOfType(ulong characterId, byte questType);

        bool UpdateMonsterProgress(ulong characterId, int questId, int monsterId, short killCount, int recordTime);

        bool RemoveActive(ulong characterId, byte questType, int questId, int monsterId);
}
