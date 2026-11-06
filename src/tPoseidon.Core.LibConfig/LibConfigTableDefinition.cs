namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigFieldDefinition(string Name, bool IsKey, string DataType);

public sealed record LibConfigTableDefinition(
    string Name,
    string Source,
    int InfoId,
    IReadOnlyList<LibConfigFieldDefinition> Fields,
    IReadOnlyList<IReadOnlyList<string>> Rows);
