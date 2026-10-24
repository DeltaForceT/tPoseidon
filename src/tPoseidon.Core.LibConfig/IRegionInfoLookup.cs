namespace tPoseidon.Core.LibConfig;

public interface IRegionInfoLookup
{
    RegionInfoEntry? GetByMapAndRegion(string mapName, int regionId);
    IReadOnlyList<RegionInfoEntry> GetByMap(string mapName);
    IReadOnlyCollection<RegionInfoEntry> All { get; }
    int Count { get; }
}
