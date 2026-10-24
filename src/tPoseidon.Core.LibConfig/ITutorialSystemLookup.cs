namespace tPoseidon.Core.LibConfig;

public interface ITutorialSystemLookup
{
        TutorialSystemEntry? GetById(int tutorialId);

        TutorialSystemEntry? FindByStartNpcOnMap(int npcType, int mapId);

        IReadOnlyList<TutorialSystemEntry> FindByQuestId(int questId);

        int Count { get; }
}
