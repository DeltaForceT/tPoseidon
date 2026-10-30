namespace tPoseidon.Core.LibConfig;

public interface INpcTalkLookup
{
        NpcTalkEntry? GetByPosOnMap(int posId, string mapName);

        NpcTalkEntry? GetByTypeAnyMap(int npcType);

        IReadOnlyList<NpcTalkEntry> GetAllByMap(string mapName);

        int Count { get; }
}
