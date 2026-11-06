namespace tPoseidon.Core.DataCatalog;

public sealed record TableDescriptor(string Name, string Family, int Occurrences, int RowCount, IReadOnlyList<string> Columns, IReadOnlyList<string> Readers);
