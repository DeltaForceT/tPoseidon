namespace tPoseidon.Core.LibConfig;

public sealed record PetLevelUpEntry(int SourceId, int ResultId, int CatalystItemId, int CatalystCount, bool Evolve);

public sealed record PetLevelUpTables(IReadOnlyDictionary<int, PetLevelUpEntry> Rows)
{
    public const string TableName = "PetLevelUpInfo";

    public static PetLevelUpTables Empty { get; } = new(new Dictionary<int, PetLevelUpEntry>());
}
