namespace tPoseidon.Core.LibConfig;

public sealed record HarconStageEntry(
    int Id,
    string Name,
    int Level,
    int Item,
    int UnlockId,
    int MinPlayers,
    int MaxPlayers,
    int WaitMap,
    int PlayMap,
    string WaveTable,
    int DropId,
    int Category);

public sealed record HarconSpawnGroup(int MonsterId, int Total, int RegionId, int Lump, int DelayMs);

public sealed record HarconWave(int Id, int Type, int TimeSeconds, int Alarm, IReadOnlyList<HarconSpawnGroup> Groups);

public sealed record HarconDropEntry(int Id, IReadOnlyList<int> RankCoins, int Other, int MinScore, int HarconTitle);

public sealed record HarconPointRule(int OneOnKill, int WaveAce, int NoDeath, int Death)
{
    public static HarconPointRule Empty { get; } = new(0, 0, 0, 0);
}

public sealed record HarconShopEntry(int Shop, int Id, int Category, int ItemId, int ItemCount, int Coins);

public sealed record HarconDefTables(
    IReadOnlyDictionary<int, HarconStageEntry> Stages,
    IReadOnlyDictionary<string, IReadOnlyList<HarconWave>> Waves,
    IReadOnlyDictionary<int, HarconDropEntry> Drops,
    HarconPointRule Points,
    IReadOnlyDictionary<(int Shop, int ItemId), HarconShopEntry> Shop,
    IReadOnlyDictionary<string, IReadOnlyList<(int MonsterId, int RegionId, int Count)>> DefendedSpawns)
{
    public const string StageTable = "HarconDefInfo";
    public const string DropTable = "HarconDefDrop";
    public const string PointTable = "HarconPointRule";
    public const string ShopTablePrefix = "HarconShop_";
    public const string WaveTablePrefix = "Sc_HarconDef_";
    public const string MonsterTablePrefix = "Mon_HarconDef_";
    public const int SpawnGroups = 20;
    public const int RankedCoins = 10;
    public const string NpcTalkTablePrefix = "NpcTalk_";
    public const string NpcShopColumn = "HarconShopTable";

    public IReadOnlyDictionary<int, int> ShopOfNpc { get; init; } = new Dictionary<int, int>();

    public HarconShopEntry? Offer(int npcType, int itemId) =>
        ShopOfNpc.TryGetValue(npcType, out var shop) && Shop.TryGetValue((shop, itemId), out var offer) ? offer : null;

    public static HarconDefTables Empty { get; } = new(
        new Dictionary<int, HarconStageEntry>(),
        new Dictionary<string, IReadOnlyList<HarconWave>>(),
        new Dictionary<int, HarconDropEntry>(),
        HarconPointRule.Empty,
        new Dictionary<(int, int), HarconShopEntry>(),
        new Dictionary<string, IReadOnlyList<(int MonsterId, int RegionId, int Count)>>());
}
