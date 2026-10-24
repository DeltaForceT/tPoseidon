namespace tPoseidon.Core.LibConfig;

public interface IMonZoneLookup
{
    IReadOnlyList<MonZoneEntry> GetByMap(string mapName);
    IReadOnlyCollection<MonZoneEntry> All { get; }
    int Count { get; }
}
