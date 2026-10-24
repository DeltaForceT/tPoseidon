using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common;

public sealed class LevelExpTable
{
    private readonly Dictionary<int, ulong> _expToNext;
    private readonly Dictionary<int, ulong> _tmxpToNext;

    public int MinLevel { get; }
    public int MaxLevelWithData { get; }
    public int MaxTmLevelWithData { get; }

    private LevelExpTable(Dictionary<int, ulong> expToNext, Dictionary<int, ulong> tmxpToNext)
    {
        _expToNext = expToNext;
        _tmxpToNext = tmxpToNext;
        MinLevel = expToNext.Count > 0 ? expToNext.Keys.Min() : 1;
        MaxLevelWithData = expToNext.Count > 0 ? expToNext.Keys.Max() : 1;
        MaxTmLevelWithData = tmxpToNext.Count > 0 ? tmxpToNext.Keys.Max() : 0;
    }

    public ulong? ExpToNextLevel(int currentLevel)
    {
        return _expToNext.TryGetValue(currentLevel, out var v) ? v : null;
    }

    public ulong? TmXpToNextLevel(int currentTmLevel)
    {
        return _tmxpToNext.TryGetValue(currentTmLevel, out var v) ? v : null;
    }

    public static LevelExpTable LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<RootDoc>(json, JsonOpts)
            ?? throw new InvalidDataException($"Failed to deserialize {path}");
        if (root.Levels is null)
        {
            throw new InvalidDataException($"{path} missing 'Levels' array");
        }

        var exp = new Dictionary<int, ulong>();
        var tm = new Dictionary<int, ulong>();
        foreach (var row in root.Levels)
        {
            if (row.Exp is { } e and > 0) exp[row.Level] = (ulong)e;
            if (row.TmXp is { } t and > 0) tm[row.Level] = (ulong)t;
        }
        return new LevelExpTable(exp, tm);
    }

    public static LevelExpTable Empty() => new(new(), new());

    public static LevelExpTable FromInlineThresholds(
        IReadOnlyDictionary<int, ulong> expToNext,
        IReadOnlyDictionary<int, ulong>? tmxpToNext = null)
    {
        return new LevelExpTable(
            new Dictionary<int, ulong>(expToNext),
            tmxpToNext is null ? new() : new Dictionary<int, ulong>(tmxpToNext));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class RootDoc
    {
        [JsonPropertyName("Levels")]
        public List<LevelRow>? Levels { get; set; }
    }

    private sealed class LevelRow
    {
        public int Level { get; set; }
        public long? Exp { get; set; }
        public long? TmXp { get; set; }
    }
}
