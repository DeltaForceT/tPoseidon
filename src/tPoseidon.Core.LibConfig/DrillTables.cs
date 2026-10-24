namespace tPoseidon.Core.LibConfig;

public sealed record DrillParamEntry(
    int Id, int Grade, int GroundNature, int DigDepth, int BarEffArea, int BarSpeed, int ScoopMass, int DrillLife, float RandomSpotRatio = 0f);

public sealed record DrillZoneEntry(int ZoneId, int MinItemDepth, int MaxItemDepth, float ItemPercentage)
{
    public bool HasItems => MinItemDepth >= 1 && MaxItemDepth >= 1 && ItemPercentage > 0;
}

public readonly record struct MapItemChance(int ItemId, int Rate);

public sealed record MapItemCategory(string TableName, int Rate);

public sealed record MapItemZoneRow
{
    public const int SpecialCategory = 0;
    public const int EquipCategory = 1;
    public const int FirstNormalCategory = 2;
    public const int TableCategories = 5;
    public const int RowCategory = 5;
    public const int Categories = 6;
    public const int ItemColumns = 30;

    public required int ZoneId { get; init; }
    public IReadOnlyList<MapItemCategory> Tables { get; init; } = Array.Empty<MapItemCategory>();
    public IReadOnlyList<MapItemChance> Items { get; init; } = Array.Empty<MapItemChance>();
}

public sealed record DrillTables
{
    public IReadOnlyDictionary<int, DrillParamEntry> Drills { get; init; } = new Dictionary<int, DrillParamEntry>();
    public IReadOnlyDictionary<int, DrillZoneEntry> Zones { get; init; } = new Dictionary<int, DrillZoneEntry>();
    public IReadOnlyDictionary<int, MapItemZoneRow> ZoneItems { get; init; } = new Dictionary<int, MapItemZoneRow>();
    public IReadOnlyDictionary<string, IReadOnlyList<MapItemChance>> ItemTables { get; init; } = new Dictionary<string, IReadOnlyList<MapItemChance>>();
    public IReadOnlyList<float> EventRateAdd { get; init; } = Array.Empty<float>();
    public IReadOnlyList<float> GradeExpRates { get; init; } = Array.Empty<float>();
    public IReadOnlyDictionary<int, int> GroundNatures { get; init; } = new Dictionary<int, int>();
    public IReadOnlyDictionary<int, int> AutoDrillPets { get; init; } = new Dictionary<int, int>();
}
