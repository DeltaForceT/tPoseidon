namespace tPoseidon.Core.LibConfig;

public sealed record WorldMapTables(IReadOnlySet<int> OpenedZones)
{
    public const string RegionTable = "WorldMap_RegionInfo";

    public static WorldMapTables Empty { get; } = new(new HashSet<int>());

    public bool Shows(int zoneId) => OpenedZones.Contains(zoneId);
}
