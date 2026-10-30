namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigRowPatch(string Table, string KeyField, IReadOnlyList<string> Fields, IReadOnlyList<IReadOnlyList<string>> Rows);
