namespace tPoseidon.Core.LibConfig;

public sealed record TreasureTeleportDestination(int ZoneId, float Ratio, int PointId);

public sealed record TreasureTeleportEntry(int ItemId, int TimeMinutes, IReadOnlyList<int> SpotMaps, IReadOnlyList<TreasureTeleportDestination> Destinations);

public sealed record TreasureLootSlot(int ItemId, float Ratio, int Count);

public sealed record TreasureItemEntry(
    int ItemId,
    int TimeMinutes,
    IReadOnlyList<int> SpotMaps,
    int Option,
    int Count,
    IReadOnlyList<TreasureLootSlot> Slots,
    int SideItem,
    int SideItemMin,
    int SideItemMax);

public sealed record RandomSpotEntry(int RegionId, IReadOnlyList<(int MonsterId, float Ratio)> Monsters, int TreasureId, float TreasureRatio, int MonsterDeadTimer);

public sealed record TreasureMapTables(
    IReadOnlyDictionary<int, TreasureTeleportEntry> Teleports,
    IReadOnlyDictionary<int, TreasureItemEntry> Items,
    IReadOnlyDictionary<int, RandomSpotEntry> RandomSpots,
    IReadOnlyDictionary<int, int> ZoneRegions,
    float RandomSpotAddedRatio)
{
    public const string RandomSpotTable = "RandomSurpriseSpotInfo";
    public const string RandomSpotEventTable = "RandomSurpriseSpotEvent";
    public const int RandomSpotMonsterColumns = 5;
    public const string TeleportTable = "TreasureMap_Teleport";
    public const string ItemTable = "TreasureMap_Item";
    public const int LootSlots = 20;
    public const int SpotMapColumns = 5;
    public const int DestinationColumns = 5;

    public static TreasureMapTables Empty { get; } = new(
        new Dictionary<int, TreasureTeleportEntry>(), new Dictionary<int, TreasureItemEntry>(), new Dictionary<int, RandomSpotEntry>(), new Dictionary<int, int>(), 0f);
}
