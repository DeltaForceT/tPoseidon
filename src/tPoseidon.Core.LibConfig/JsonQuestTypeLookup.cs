using System.Text.Json;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonQuestTypeLookup : IQuestTypeLookup
{
    public const byte FallbackQuestType = 1;

    private readonly IReadOnlyDictionary<int, byte> _types;

    public JsonQuestTypeLookup(IReadOnlyDictionary<int, byte> types)
    {
        ArgumentNullException.ThrowIfNull(types);
        _types = types;
    }

    public int Count => _types.Count;

    public byte GetQuestType(int questId)
        => _types.TryGetValue(questId, out var t) ? t : FallbackQuestType;

    public static JsonQuestTypeLookup LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"quest-types.json not found at {path}", path);
        }
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, byte>>(json)
            ?? throw new InvalidDataException($"quest-types.json at {path} parsed to null");
        var typed = raw.ToDictionary(
            kv => int.Parse(kv.Key, System.Globalization.CultureInfo.InvariantCulture),
            kv => kv.Value);
        return new JsonQuestTypeLookup(typed);
    }
}
