using System.Text.Json;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonNpcPointProvider : INpcPointProvider
{
    private readonly Dictionary<(ushort MapId, ushort PosId), NpcPoint> _points;

    public JsonNpcPointProvider(IReadOnlyDictionary<ushort, IReadOnlyDictionary<ushort, NpcPoint>> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _points = new Dictionary<(ushort, ushort), NpcPoint>();
        foreach (var (mapId, perMap) in data)
        {
            foreach (var (posId, point) in perMap)
            {
                _points[(mapId, posId)] = point;
            }
        }
    }

    public bool TryGet(ushort mapId, ushort posId, out NpcPoint point)
        => _points.TryGetValue((mapId, posId), out point);

        public int Count => _points.Count;

        public int MapCount => _points.Keys.Select(k => k.MapId).Distinct().Count();

        public static JsonNpcPointProvider LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"NPC point catalog not found at {path}. " +
                "Run scripts/extract-npc-points.py to generate it.", path);
        }

        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, RawPointRecord>>>(json)
            ?? throw new InvalidDataException($"NPC point catalog at {path} parsed to null.");

        var data = new Dictionary<ushort, IReadOnlyDictionary<ushort, NpcPoint>>();
        foreach (var (mapKey, perMap) in raw)
        {
            if (!ushort.TryParse(mapKey, out var mapId))
            {
                continue;  
            }
            var perMapPoints = new Dictionary<ushort, NpcPoint>();
            foreach (var (posKey, rec) in perMap)
            {
                if (!ushort.TryParse(posKey, out var posId)) continue;
                perMapPoints[posId] = NpcPoint.Xy(rec.X, rec.Y);
            }
            data[mapId] = perMapPoints;
        }

        return new JsonNpcPointProvider(data);
    }

        private sealed class RawPointRecord
    {
        public int Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
