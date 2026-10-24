namespace tPoseidon.Core.LibConfig;

public interface IMapItemZoneLookup
{
    IReadOnlyList<MapItemZoneEntry> GetByMap(string mapName);
    IReadOnlyCollection<MapItemZoneEntry> All { get; }
    int Count { get; }
}
