namespace tPoseidon.Core.LibConfig;

public sealed record SoulSeedEventEntry(int Index, int EventType, int QuestType, int QuestId, bool Special, int MaximumLevel, int Rate, IReadOnlyList<int> Points);

public sealed record SoulSeedTables(
    IReadOnlyDictionary<int, IReadOnlyList<int>> GuardianTypesBySeed,
    IReadOnlyDictionary<int, SoulSeedEventEntry> EventsByType,
    IReadOnlyDictionary<int, string> GuardianNames)
{
    public const string SeedTable = "SoulSeedParam";
    public const string EventTable = "SoulSeedEventInfo";
    public const string GuardianTable = "SoulGuardianParam";
    public const int GuardianKinds = 4;

    public static readonly string[] SeedTypeColumns = ["FType_Power", "FType_Magic", "FType_Sense", "FType_Charm"];
    public static readonly string[] PointColumns = ["point_power", "point_magic", "point_sense", "point_charm"];

    public static SoulSeedTables Empty { get; } = new(
        new Dictionary<int, IReadOnlyList<int>>(),
        new Dictionary<int, SoulSeedEventEntry>(),
        new Dictionary<int, string>());
}
