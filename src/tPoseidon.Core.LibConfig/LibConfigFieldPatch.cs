namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigFieldPatch(string Table, string KeyField, string Key, IReadOnlyDictionary<string, string> Values);
