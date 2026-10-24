namespace tPoseidon.Core.LibConfig;

public sealed record FiestaSlotEntry(
    int Id,
    int ZoneId,
    int PortalIndex,
    int PortalOpenMinute,
    int InYear,
    int InMonth,
    int InDay,
    int InMinuteOfDay,
    int OutMinuteOfDay);

public sealed record LevelPortBand(int ZoneId, int MinLevel, int MaxLevel);

public sealed record LevelPortEntry(int Id, string Notice, int PortType, IReadOnlyList<LevelPortBand> Bands);

public sealed record FiestaTables(IReadOnlyList<FiestaSlotEntry> Slots, IReadOnlyDictionary<int, LevelPortEntry> LevelPorts)
{
    public const string SlotTable = "FiestaInfo";
    public const string LevelPortTable = "LevelPortParam";
    public const int LevelPortBands = 20;

    public static IReadOnlySet<string> TableNames { get; } = new HashSet<string>(StringComparer.Ordinal) { SlotTable, LevelPortTable };

    public static FiestaTables Empty { get; } = new(Array.Empty<FiestaSlotEntry>(), new Dictionary<int, LevelPortEntry>());
}
