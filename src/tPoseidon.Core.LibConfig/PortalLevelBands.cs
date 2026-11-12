namespace tPoseidon.Core.LibConfig;

public readonly record struct PortalLevelBand(
    int MinLevel,
    int MaxLevel,
    int CheckType = 0,
    int JobType1 = 0,
    int JobType2 = 0,
    int QuestType = 0,
    int QuestId = 0,
    int ItemId = 0,
    int DestZone = 0)
{
    public bool Redirects => DestZone > 0;
}

public sealed class PortalLevelBands
{
    public const string TableName = "PortalInfo";

    private readonly IReadOnlyDictionary<(int Map, int Portal), PortalLevelBand> _bands;

    public PortalLevelBands(IReadOnlyDictionary<(int Map, int Portal), PortalLevelBand> bands)
    {
        _bands = bands ?? throw new ArgumentNullException(nameof(bands));
    }

    public static PortalLevelBands Empty { get; } = new(new Dictionary<(int Map, int Portal), PortalLevelBand>());

    public int Count => _bands.Count;

    public PortalLevelBand? Find(int map, int portal) => _bands.TryGetValue((map, portal), out var band) ? band : null;
}
