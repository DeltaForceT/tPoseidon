using System.Globalization;

namespace tPoseidon.Core.LibConfig;

public sealed record SkillTableRow
{
    public required string Table { get; init; }
    public required int Level { get; init; }
    public required IReadOnlyDictionary<string, string> Columns { get; init; }

    public bool Has(string column) => Columns.ContainsKey(column);

    public int? Amount(string column) =>
        Columns.TryGetValue(column, out var raw)
        && int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    public float? Value(string column) =>
        Columns.TryGetValue(column, out var raw)
        && float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
