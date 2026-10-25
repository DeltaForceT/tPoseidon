namespace tPoseidon.Core.LibConfig;

public readonly record struct PortalLevelBand(int MinLevel, int MaxLevel);

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
