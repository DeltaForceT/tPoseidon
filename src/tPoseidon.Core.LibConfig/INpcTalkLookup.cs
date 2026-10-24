namespace tPoseidon.Core.LibConfig;

public interface INpcTalkLookup
{
        NpcTalkEntry? GetByTypeOnMap(int npcType, string mapName);

        NpcTalkEntry? GetByTypeAnyMap(int npcType);

        IReadOnlyList<NpcTalkEntry> GetAllByMap(string mapName);

        int Count { get; }
}
