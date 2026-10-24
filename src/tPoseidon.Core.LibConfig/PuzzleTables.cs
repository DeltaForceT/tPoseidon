namespace tPoseidon.Core.LibConfig;

public sealed record PuzzleMapEntry(int Uid, int MapId, int PuzzleType, int PuzzleId);

public sealed record MemoryPuzzleEntry(
    int Id,
    int TurnSeconds,
    string ObjectTable,
    int ObjectCount,
    int TimeLimitSeconds,
    int PortalMap,
    IReadOnlyList<(int ItemId, int Count)> Rewards);

public sealed record SlidePuzzleEntry(
    int Id,
    string BlockTable,
    int Columns,
    int Rows,
    int TimeLimitSeconds,
    int PortalMap,
    IReadOnlyList<(int ItemId, int Count)> Rewards);

public sealed record SlidePuzzleBlock(int Id, int Bundle)
{
    public bool IsEmpty => Bundle == PuzzleTables.EmptyBlockBundle;
}

public sealed record PuzzleTables(
    IReadOnlyDictionary<int, PuzzleMapEntry> Maps,
    IReadOnlyDictionary<int, MemoryPuzzleEntry> Memory,
    IReadOnlyDictionary<string, IReadOnlyList<(int PositionId, int MonsterId)>> MemoryObjects,
    IReadOnlyDictionary<int, SlidePuzzleEntry> Slide,
    IReadOnlyDictionary<string, IReadOnlyList<SlidePuzzleBlock>> SlideBlocks)
{
    public const string MapTable = "PuzzleMapParam";
    public const string MemoryTable = "MemoryPuzzleParam";
    public const string MemoryObjectPrefix = "MemoryPuzzleObject_";
    public const string SlideTable = "SlidePuzzleParam";
    public const string SlideBlockPrefix = "SlidePuzzleBlock_";
    public const int EmptyBlockBundle = 10000;
    public const int SlideType = 0;
    public const int MemoryType = 1;
    public const int RewardColumns = 4;

    public static PuzzleTables Empty { get; } = new(
        new Dictionary<int, PuzzleMapEntry>(),
        new Dictionary<int, MemoryPuzzleEntry>(),
        new Dictionary<string, IReadOnlyList<(int, int)>>(),
        new Dictionary<int, SlidePuzzleEntry>(),
        new Dictionary<string, IReadOnlyList<SlidePuzzleBlock>>());
}
