namespace tPoseidon.Core.LibConfig;

public sealed record TeleportAdvEntry(int ZoneIndex, int ZoneType, int ZoneId);

public sealed record RandomPortZone(int ZoneId, int Ratio);

public sealed record RandomPortEntry(int ItemId, string Notice, IReadOnlyList<RandomPortZone> Zones);

public sealed record SpecialPortTables(
    IReadOnlyDictionary<int, TeleportAdvEntry> Advanced,
    IReadOnlyDictionary<int, RandomPortEntry> Random)
{
    public const string AdvancedTable = "TeleportAdvInfo";
    public const string RandomTable = "RandomPortParam";
    public const int RandomZoneSlots = 20;

    public static IReadOnlySet<string> TableNames { get; } = new HashSet<string>(StringComparer.Ordinal) { AdvancedTable, RandomTable };

    public static SpecialPortTables Empty { get; } = new(new Dictionary<int, TeleportAdvEntry>(), new Dictionary<int, RandomPortEntry>());
}
