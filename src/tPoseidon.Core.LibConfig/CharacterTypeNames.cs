namespace tPoseidon.Core.LibConfig;

public sealed record CharacterTypeNames(IReadOnlyDictionary<int, string> Species, IReadOnlyDictionary<int, string> Jobs)
{
    public const string SpeciesTable = "ChrTypeInfo";
    public const string JobTable = "JobTypeInfo";
    public const int SpeciesCount = 9;
    public const int Tiers = 3;

    public static CharacterTypeNames Empty { get; } = new(new Dictionary<int, string>(), new Dictionary<int, string>());

    public static int SpeciesOf(int characterType) => (characterType - 1) % SpeciesCount + 1;

    public static int TierOf(int characterType) => (characterType - 1) / SpeciesCount + 1;
}
