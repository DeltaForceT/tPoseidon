using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.LibConfig;

public sealed class JsonPortalLookup : IPortalLookup
{
    private readonly Dictionary<ushort, PortalEntrance[]> _entrancesByMap;
    private readonly Dictionary<(ushort DestMap, ushort FromMap), (ushort X, ushort Y)> _arrivalByPair;
    private readonly Dictionary<ushort, (ushort X, ushort Y)[]> _entryPointsByMap;
    private readonly Random _rng;

    public JsonPortalLookup(
        IReadOnlyDictionary<ushort, IReadOnlyList<PortalEntrance>> entrancesByMap,
        IReadOnlyDictionary<(ushort, ushort), (ushort, ushort)> arrivalByPair,
        IReadOnlyDictionary<ushort, IReadOnlyList<(ushort X, ushort Y)>>? entryPointsByMap = null,
        Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(entrancesByMap);
        ArgumentNullException.ThrowIfNull(arrivalByPair);
        _entrancesByMap = entrancesByMap.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        _arrivalByPair = arrivalByPair.ToDictionary(kv => kv.Key, kv => kv.Value);
        _entryPointsByMap = entryPointsByMap?.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray())
            ?? new Dictionary<ushort, (ushort X, ushort Y)[]>();
        _rng = rng ?? Random.Shared;
    }

    public int MapCount => _entrancesByMap.Count;

    public int TotalPortals => _entrancesByMap.Values.Sum(arr => arr.Length);

    public IReadOnlyList<PortalEntrance> GetEntrances(ushort mapId) =>
        _entrancesByMap.TryGetValue(mapId, out var entrances)
            ? entrances
            : Array.Empty<PortalEntrance>();

    public (ushort X, ushort Y) FindArrival(ushort fromMapId, ushort destMapId)
    {
        (ushort X, ushort Y)? direct = _arrivalByPair.TryGetValue((destMapId, fromMapId), out var spawn) ? spawn : null;
        var entryPoints = _entryPointsByMap.TryGetValue(destMapId, out var points) ? points : Array.Empty<(ushort X, ushort Y)>();
        lock (_rng) return PortalArrivalRule.Resolve(direct, entryPoints, _rng);
    }

    public PortalDestination? FindDestinationWithin(ushort sourceMapId, int posX, int posY, int margin)
    {
        if (!_entrancesByMap.TryGetValue(sourceMapId, out var entrances)) return null;
        foreach (var e in entrances)
        {
            if (posX >= e.XMin - margin && posX <= e.XMax + margin && posY >= e.YMin - margin && posY <= e.YMax + margin)
            {
                return DestinationOf(e, sourceMapId);
            }
        }
        return null;
    }

    public PortalDestination? FindDestination(ushort sourceMapId, int posX, int posY)
    {
        if (!_entrancesByMap.TryGetValue(sourceMapId, out var entrances)) return null;

        PortalEntrance? hit = null;
        foreach (var e in entrances)
        {
            if (posX >= e.XMin && posX <= e.XMax && posY >= e.YMin && posY <= e.YMax)
            {
                hit = e;
                break;
            }
        }
        if (hit is null)
        {
            const int nearestThreshold = 200;
            int bestDist = int.MaxValue;
            foreach (var e in entrances)
            {
                var dx = Math.Max(0, Math.Max(e.XMin - posX, posX - e.XMax));
                var dy = Math.Max(0, Math.Max(e.YMin - posY, posY - e.YMax));
                var dist = dx + dy;
                if (dist < bestDist) { bestDist = dist; hit = e; }
            }
            if (bestDist > nearestThreshold) hit = null;
        }
        return hit is null ? null : DestinationOf(hit, sourceMapId);
    }

    private PortalDestination DestinationOf(PortalEntrance hit, ushort sourceMapId)
    {
        var spawn = FindArrival(sourceMapId, hit.DestMap);
        return new PortalDestination
        {
            DestMapId = hit.DestMap,
            DestPortalId = hit.Id,
            SpawnX = spawn.X,
            SpawnY = spawn.Y,
        };
    }

    public const int ArrivalPointType = 1;
    public const int OldArrivalPointType = 0;

    private static readonly int[] ArrivalPointTypes = [ArrivalPointType, OldArrivalPointType];

    public static JsonPortalLookup LoadFromMapFiles(IEnumerable<MapInfoEntry> maps, string mapDirectory)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentException.ThrowIfNullOrEmpty(mapDirectory);
        var entrances = new Dictionary<ushort, IReadOnlyList<PortalEntrance>>();
        var arrivals = new Dictionary<(ushort, ushort), (ushort, ushort)>();
        var entryPoints = new Dictionary<ushort, IReadOnlyList<(ushort X, ushort Y)>>();
        foreach (var map in maps)
        {
            var fileName = map.Md3FileName.Trim();
            if (fileName.Length == 0) continue;
            if (Md3MapReader.ReadMap(tPoseidon.Core.Common.ClientFiles.Combine(mapDirectory, fileName)) is not { } md3) continue;
            AddMap(map.Id, md3, entrances, arrivals, entryPoints);
        }
        return new JsonPortalLookup(entrances, arrivals, entryPoints);
    }

    public static void AddMap(
        ushort mapId,
        Md3Map md3,
        Dictionary<ushort, IReadOnlyList<PortalEntrance>> entrances,
        Dictionary<(ushort, ushort), (ushort, ushort)> arrivals,
        Dictionary<ushort, IReadOnlyList<(ushort X, ushort Y)>>? entryPoints = null)
    {
        ArgumentNullException.ThrowIfNull(md3);
        ArgumentNullException.ThrowIfNull(entrances);
        ArgumentNullException.ThrowIfNull(arrivals);
        if (entryPoints is not null)
        {
            var entries = md3.Points
                .Where(point => point.Type == ArrivalPointType)
                .Select(point => ((ushort)Math.Clamp(point.X, 0, ushort.MaxValue), (ushort)Math.Clamp(point.Y, 0, ushort.MaxValue)))
                .ToArray();
            if (entries.Length > 0) entryPoints[mapId] = entries;
        }
        var list = md3.Ranges
            .Where(range => range.Destination is > 0 and <= ushort.MaxValue)
            .Select(range => new PortalEntrance
            {
                Id = range.Id,
                XMin = Math.Min(range.X1, range.X2),
                YMin = Math.Min(range.Y1, range.Y2),
                XMax = Math.Max(range.X1, range.X2),
                YMax = Math.Max(range.Y1, range.Y2),
                DestMap = (ushort)range.Destination,
            })
            .ToList();
        if (list.Count > 0) entrances[mapId] = list;
        foreach (var pointType in ArrivalPointTypes)
        {
            foreach (var point in md3.Points)
            {
                if (point.Type != pointType || point.MapId is <= 0 or > ushort.MaxValue) continue;
                arrivals.TryAdd((mapId, (ushort)point.MapId), ((ushort)point.X, (ushort)point.Y));
            }
        }
    }

    public static JsonPortalLookup LoadFromPortalsJson(string portalsJsonPath)
    {
        ArgumentNullException.ThrowIfNull(portalsJsonPath);
        var json = File.ReadAllText(portalsJsonPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, MapPortalEntry>>(json, JsonOpts)
            ?? throw new InvalidDataException($"Empty or invalid portals JSON at {portalsJsonPath}");

        var entrances = new Dictionary<ushort, IReadOnlyList<PortalEntrance>>();
        var arrivals = new Dictionary<(ushort, ushort), (ushort, ushort)>();

        foreach (var (mapKey, entry) in raw)
        {
            if (!ushort.TryParse(mapKey, out var mapId)) continue;

            if (entry.Entrances is not null && entry.Entrances.Length > 0)
            {
                var list = new List<PortalEntrance>(entry.Entrances.Length);
                foreach (var e in entry.Entrances)
                {
                    list.Add(new PortalEntrance
                    {
                        Id = e.Id,
                        XMin = e.X1,
                        YMin = e.Y1,
                        XMax = e.X2,
                        YMax = e.Y2,
                        DestMap = (ushort)e.DestMapId,
                    });
                }
                entrances[mapId] = list;
            }

            if (entry.Arrivals is not null && entry.Arrivals.Length > 0)
            {
                foreach (var a in entry.Arrivals)
                {
                    if (a.FromMapId <= 0 || a.FromMapId > ushort.MaxValue) continue;
                    arrivals[(mapId, (ushort)a.FromMapId)] = ((ushort)a.X, (ushort)a.Y);
                }
            }
        }
        return new JsonPortalLookup(entrances, arrivals);
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class MapPortalEntry
    {
        [JsonPropertyName("MapName")] public string? MapName { get; init; }
        [JsonPropertyName("Md3FileName")] public string? Md3FileName { get; init; }
        [JsonPropertyName("Entrances")] public EntranceRaw[]? Entrances { get; init; }
        [JsonPropertyName("Arrivals")] public ArrivalRaw[]? Arrivals { get; init; }
    }

    private sealed class EntranceRaw
    {
        public int Id { get; init; }
        public int DestMapId { get; init; }
        public int X1 { get; init; }
        public int Y1 { get; init; }
        public int X2 { get; init; }
        public int Y2 { get; init; }
    }

    private sealed class ArrivalRaw
    {
        public int FromMapId { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
    }
}

public sealed record PortalEntrance
{
    public int Id { get; init; }
    public required int XMin { get; init; }
    public required int YMin { get; init; }
    public required int XMax { get; init; }
    public required int YMax { get; init; }
    public required ushort DestMap { get; init; }
}
