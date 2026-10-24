namespace tPoseidon.Core.LibConfig;

public sealed record CharacterPetReinforceEntry(int ResultId, int ReinforceItemId, bool RareType, int BasicId, IReadOnlyList<int> Stats);

public sealed record CharacterPetReinforceTables(IReadOnlyDictionary<(int ReinforceItemId, int BasicId), CharacterPetReinforceEntry> Rows)
{
    public const string TableName = "CharacterPetReinforceInfo";
    public static readonly string[] StatColumns = ["AP", "AC", "DX", "MP", "MA", "MD", "WT", "DA", "LK", "HP", "DP", "HV", "GUN"];

    public static CharacterPetReinforceTables Empty { get; } = new(new Dictionary<(int, int), CharacterPetReinforceEntry>());
}
