using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonMonsterSpawnProvider : IMonsterSpawnProvider
{
    private readonly Dictionary<ushort, MonsterSpawnPoint[]> _byMap;

    public JsonMonsterSpawnProvider(IReadOnlyDictionary<ushort, IReadOnlyList<MonsterSpawnPoint>> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _byMap = data.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    public IReadOnlyList<MonsterSpawnPoint> GetSpawnPoints(ushort mapId) =>
        _byMap.TryGetValue(mapId, out var pts) ? pts : Array.Empty<MonsterSpawnPoint>();

    public IReadOnlyCollection<ushort> KnownMaps => _byMap.Keys;

    public int TotalPoints => _byMap.Values.Sum(arr => arr.Length);

    public int MapCount => _byMap.Count;

    public static JsonMonsterSpawnProvider LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, MapEntry>>(json, JsonOpts)
            ?? throw new InvalidDataException($"Empty or invalid spawn JSON at {path}");

        var data = new Dictionary<ushort, IReadOnlyList<MonsterSpawnPoint>>();
        foreach (var (mapKey, entry) in raw)
        {
            if (!ushort.TryParse(mapKey, out var mapId)) continue;
            if (entry.Points is null || entry.Points.Length == 0) continue;
            var points = new List<MonsterSpawnPoint>(entry.Points.Length);
            foreach (var p in entry.Points)
            {
                if (p.X > ushort.MaxValue || p.Y > ushort.MaxValue) continue;
                points.Add(new MonsterSpawnPoint
                {
                    Id = p.Id,
                    X = (ushort)p.X,
                    Y = (ushort)p.Y,
                });
            }
            if (points.Count > 0) data[mapId] = points;
        }
        return new JsonMonsterSpawnProvider(data);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class MapEntry
    {
        [JsonPropertyName("ranges")]
        public RangeEntry[]? Ranges { get; init; }

        [JsonPropertyName("points")]
        public PointEntry[]? Points { get; init; }
    }

    private sealed class PointEntry
    {
        public int Id { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
    }

    private sealed class RangeEntry
    {
        public int Id { get; init; }
        public int X1 { get; init; }
        public int Y1 { get; init; }
        public int X2 { get; init; }
        public int Y2 { get; init; }
    }
}
