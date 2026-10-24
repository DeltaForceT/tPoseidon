using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonZoneMonsterPoolLookup : IZoneMonsterPoolLookup
{
    public const int ShadowRealmMonsterIdThreshold = 16000;

    private readonly Dictionary<ushort, ZoneMonsterPool> _byMap;

    public JsonZoneMonsterPoolLookup(IReadOnlyDictionary<ushort, ZoneMonsterPool> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _byMap = new Dictionary<ushort, ZoneMonsterPool>(data);
    }

    public ZoneMonsterPool? GetByMap(ushort mapId) =>
        _byMap.TryGetValue(mapId, out var z) ? z : null;

    public IReadOnlyCollection<ushort> KnownMaps => _byMap.Keys;

    public int TotalPoolRows => _byMap.Values.Sum(z => z.Pool.Count);

    public int MapCount => _byMap.Count;

    public static JsonZoneMonsterPoolLookup LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, ZoneRaw>>(json, JsonOpts)
            ?? throw new InvalidDataException($"Empty or invalid zone pool JSON at {path}");

        var data = new Dictionary<ushort, ZoneMonsterPool>();
        foreach (var (mapKey, entry) in raw)
        {
            if (!ushort.TryParse(mapKey, out var mapId)) continue;
            if (entry.Pool is null || entry.Pool.Length == 0) continue;
            var pool = new List<ZoneMonsterPoolEntry>(entry.Pool.Length);
            foreach (var p in entry.Pool)
            {
                if (p.MonsterID <= 0) continue;
                if (p.MonsterID >= ShadowRealmMonsterIdThreshold) continue;
                pool.Add(new ZoneMonsterPoolEntry
                {
                    MonsterID = p.MonsterID,
                    Ratio = p.Ratio,
                    Avr = p.Avr,
                    Max = p.Max,
                });
            }
            if (pool.Count == 0) continue;
            data[mapId] = new ZoneMonsterPool
            {
                MapId = mapId,
                MapName = entry.MapName ?? string.Empty,
                MonsterTable = entry.MonsterTable ?? string.Empty,
                MonsterAvrNum = entry.MonsterAvrNum,
                Pool = pool,
            };
        }
        return new JsonZoneMonsterPoolLookup(data);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class ZoneRaw
    {
        [JsonPropertyName("MapName")] public string? MapName { get; init; }
        [JsonPropertyName("MonsterTable")] public string? MonsterTable { get; init; }
        [JsonPropertyName("MonsterAvrNum")] public int MonsterAvrNum { get; init; }
        [JsonPropertyName("Pool")] public PoolRaw[]? Pool { get; init; }
    }

    private sealed class PoolRaw
    {
        public int MonsterID { get; init; }
        public double Ratio { get; init; }
        public int Avr { get; init; }
        public int Max { get; init; }
    }
}
