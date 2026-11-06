using System.Globalization;

namespace tPoseidon.Core.DataCatalog.Store;

public static class RowValues
{
    public static int ReadInt(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        return int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    public static float ReadFloat(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0f;
        return float.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
    }

    public static double ReadDouble(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0.0;
        return double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0.0;
    }

    public static string Text(IReadOnlyDictionary<string, string> row, string field) =>
        row.TryGetValue(field, out var value) ? value.Trim() : string.Empty;

    public static int Flags(IReadOnlyDictionary<string, string> row, string field)
    {
        if (!row.TryGetValue(field, out var raw) || string.IsNullOrWhiteSpace(raw)) return 0;
        var flags = 0;
        foreach (var part in raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flag)) flags |= flag;
        }
        return flags;
    }
}
