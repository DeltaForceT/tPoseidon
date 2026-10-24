using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonMapInfoLookup : IMapInfoLookup
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly Dictionary<ushort, MapInfoEntry> _byId;

    public JsonMapInfoLookup(IReadOnlyDictionary<ushort, MapInfoEntry> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _byId = new Dictionary<ushort, MapInfoEntry>(data);
    }

    public MapInfoEntry? GetById(ushort mapId) =>
        _byId.TryGetValue(mapId, out var entry) ? entry : null;

    public IReadOnlyCollection<MapInfoEntry> All => _byId.Values;

    public int Count => _byId.Count;

    public static JsonMapInfoLookup LoadFromFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, MapInfoRaw>>(json, JsonOpts)
            ?? throw new InvalidDataException($"Empty or invalid map-info JSON at {path}");

        var data = new Dictionary<ushort, MapInfoEntry>();
        foreach (var (mapKey, entry) in raw)
        {
            if (!ushort.TryParse(mapKey, out var mapId)) continue;
            data[mapId] = new MapInfoEntry
            {
                Id = mapId,
                Name = entry.Name ?? string.Empty,
                Md3FileName = (entry.Md3FileName ?? string.Empty).Trim(),
                NpcTable = entry.NpcTable ?? string.Empty,
                MonsterTable = entry.MonsterTable ?? string.Empty,
                BossMonsterTable = entry.BossMonsterTable ?? string.Empty,
                ItemTable = entry.ItemTable ?? string.Empty,
                ShopTable = entry.ShopTable ?? string.Empty,
                SkillShopTable = entry.SkillShopTable ?? string.Empty,
                MonsterAvrNum = entry.MonsterAvrNum,
                MaxDepth = entry.MaxDepth,
                BossMonsterCnt = entry.BossMonsterCnt,
                BossMonCheckTime = entry.BossMonCheckTime,
            };
        }
        return new JsonMapInfoLookup(data);
    }

    private sealed class MapInfoRaw
    {
        public string? Name { get; set; }
        public string? Md3FileName { get; set; }
        public string? NpcTable { get; set; }
        public string? MonsterTable { get; set; }
        public string? BossMonsterTable { get; set; }
        public string? ItemTable { get; set; }
        public string? ShopTable { get; set; }
        public string? SkillShopTable { get; set; }
        public int MonsterAvrNum { get; set; }
        public int MaxDepth { get; set; }
        public int BossMonsterCnt { get; set; }
        public int BossMonCheckTime { get; set; }
    }
}
