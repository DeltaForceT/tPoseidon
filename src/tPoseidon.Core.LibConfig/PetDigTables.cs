namespace tPoseidon.Core.LibConfig;

public sealed record PetDigPetEntry(int RefineIndex, int BagSize, int DigRate);

public sealed record PetDigFoodEntry(int ItemId, int SpeedSeconds, int MaxItems, int PlusRate);

public readonly record struct PetDigExItem(int ItemId, int Rate, int Count);

public sealed record PetDigExEntry(int PetItemId, int ExRate, IReadOnlyList<PetDigExItem> Items);

public sealed record PetDigAreaEntry(int Area, int MinLevel, int MaxLevel);

public sealed record PetDigZone(int Region, int SubType, int MinItemDepth, int MaxItemDepth)
{
    public const int MineSubType = 4;

    public bool IsMine => SubType == MineSubType;
}

public readonly record struct PetDigDepthBand(int MinDepth, int MaxDepth, int Variable);

public sealed record PetDigTables(
    IReadOnlyDictionary<int, PetDigPetEntry> Pets,
    IReadOnlyDictionary<int, PetDigFoodEntry> Foods,
    IReadOnlyDictionary<int, PetDigExEntry> ExItems,
    IReadOnlyDictionary<int, PetDigAreaEntry> Areas,
    IReadOnlyDictionary<int, PetDigZone> Zones,
    IReadOnlyList<PetDigDepthBand> DepthBands)
{
    public const string PetTable = "PetDigParam";
    public const string FoodTable = "PetDigItemParam";
    public const string ExTable = "PetDigExParam";
    public const string AreaTable = "PetDigArea";
    public const string ZoneTable = "MapInfoEx";
    public const string DepthTable = "PetDigDeepParam";
    public const int SpecialDigSubType = 80;
    public const int ExItemColumns = 10;

    public static PetDigTables Empty { get; } = new(
        new Dictionary<int, PetDigPetEntry>(),
        new Dictionary<int, PetDigFoodEntry>(),
        new Dictionary<int, PetDigExEntry>(),
        new Dictionary<int, PetDigAreaEntry>(),
        new Dictionary<int, PetDigZone>(),
        Array.Empty<PetDigDepthBand>());
}
