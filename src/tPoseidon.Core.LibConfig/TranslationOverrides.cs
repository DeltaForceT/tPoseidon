using System.Collections.Concurrent;
using System.Text.Json;

namespace tPoseidon.Core.LibConfig;

public sealed class TranslationOverrides
{
    public const string FileName = "translations.en.json";

    private static readonly ConcurrentDictionary<string, TranslationOverrides> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly TranslationOverrides Empty = new(
        new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(StringComparer.Ordinal));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, string>>> _byTable;

    private TranslationOverrides(
        IReadOnlyDictionary<string, Dictionary<string, Dictionary<string, string>>> byTable)
    {
        _byTable = byTable;
    }

    public static TranslationOverrides None => Empty;

    public int OverriddenCellCount
    {
        get
        {
            var n = 0;
            foreach (var rows in _byTable.Values)
            {
                foreach (var fields in rows.Values)
                {
                    n += fields.Count;
                }
            }
            return n;
        }
    }

    public static TranslationOverrides ForLibConfigSource(string libconfigSourcePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigSourcePath);

        var directory = Path.GetDirectoryName(Path.GetFullPath(libconfigSourcePath));
        if (string.IsNullOrEmpty(directory))
        {
            return Empty;
        }

        var jsonPath = Path.Combine(directory, FileName);
        return Cache.GetOrAdd(jsonPath, Load);
    }

    private static TranslationOverrides Load(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            return Empty;
        }

        using var stream = File.OpenRead(jsonPath);

        var parsed = JsonSerializer.Deserialize<
            Dictionary<string, Dictionary<string, Dictionary<string, string>>>>(stream, JsonOptions);

        if (parsed is null || parsed.Count == 0)
        {
            return Empty;
        }

        var byTable = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>(
            StringComparer.Ordinal);

        foreach (var (table, rows) in parsed)
        {
            if (rows is null || rows.Count == 0)
            {
                continue;
            }

            var rowMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            foreach (var (rowKey, fields) in rows)
            {
                if (fields is null || fields.Count == 0)
                {
                    continue;
                }

                var fieldMap = new Dictionary<string, string>(fields.Count, StringComparer.Ordinal);
                foreach (var (field, value) in fields)
                {
                    if (value is not null)
                    {
                        fieldMap[field] = value;
                    }
                }

                if (fieldMap.Count > 0)
                {
                    rowMap[rowKey] = fieldMap;
                }
            }

            if (rowMap.Count > 0)
            {
                byTable[table] = rowMap;
            }
        }

        return byTable.Count == 0 ? Empty : new TranslationOverrides(byTable);
    }

    public string Apply(string tableName, string rowKey, string fieldName, string sourceValue)
    {
        if (_byTable.Count == 0)
        {
            return sourceValue;
        }

        if (_byTable.TryGetValue(tableName, out var rows)
            && rows.TryGetValue(rowKey, out var fields)
            && fields.TryGetValue(fieldName, out var translated))
        {
            return translated;
        }

        return sourceValue;
    }

    public string Apply(string tableName, int rowId, string fieldName, string sourceValue)
        => Apply(tableName, rowId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fieldName, sourceValue);
}
