namespace tPoseidon.Core.LibConfig;

public interface IMapInfoLookup
{
    MapInfoEntry? GetById(ushort mapId);
    IReadOnlyCollection<MapInfoEntry> All { get; }
    int Count { get; }
}
