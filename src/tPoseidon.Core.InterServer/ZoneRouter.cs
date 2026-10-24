using System.Text.Json;
using System.Text.Json.Serialization;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.InterServer;

public sealed class ZoneRoutingTable
{
    private readonly IReadOnlyDictionary<ushort, ushort> _zoneToServerIndex;
    private readonly ushort _defaultServerIndex;

    public ZoneRoutingTable(
        IReadOnlyDictionary<ushort, ushort> zoneToServerIndex,
        ushort defaultServerIndex)
    {
        _zoneToServerIndex = zoneToServerIndex ?? throw new ArgumentNullException(nameof(zoneToServerIndex));
        _defaultServerIndex = defaultServerIndex;
    }

    public int Count => _zoneToServerIndex.Count;
    public ushort DefaultServerIndex => _defaultServerIndex;

    public ServerIdentity ResolveGameServer(ushort zoneId)
    {
        var idx = _zoneToServerIndex.TryGetValue(zoneId, out var mapped)
            ? mapped
            : _defaultServerIndex;
        return idx switch
        {
            2 => ServerIdentity.Game1,
            3 => ServerIdentity.Game2,
            _ => ZoneRouter.ResolveGameServer(zoneId),
        };
    }

    public ushort[] GetZonesForServer(ushort serverIndex)
    {
        var list = new List<ushort>();
        foreach (var (zoneId, idx) in _zoneToServerIndex)
        {
            if (idx == serverIndex) list.Add(zoneId);
        }
        list.Sort();
        return list.ToArray();
    }

    public static ZoneRoutingTable LoadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<ZoneRoutingFile>(json, JsonOpts)
            ?? throw new InvalidDataException($"empty or invalid zone-routing JSON at {path}");

        var map = new Dictionary<ushort, ushort>();
        if (dto.Zones is not null)
        {
            foreach (var (key, idx) in dto.Zones)
            {
                if (!ushort.TryParse(key, out var zone)) continue;
                map[zone] = idx;
            }
        }
        var defaultIdx = dto.DefaultServerIndex == 0 ? (ushort)2 : dto.DefaultServerIndex;
        return new ZoneRoutingTable(map, defaultIdx);
    }

    public static ZoneRoutingTable LoadFromIni(IniFile ini, int worldIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(ini);
        var section = FindWorldSection(ini, worldIndex)
            ?? throw new InvalidDataException($"INI missing [world {worldIndex}] section");

        var map = new Dictionary<ushort, ushort>();
        foreach (var route in IniZoneRouteParser.ParseZoneRoutes(section))
        {
            if (route.ZoneId < 0 || route.ZoneId > ushort.MaxValue) continue;
            if (!TryResolveGameServerIndex(route.ServerName, out var idx)) continue;
            map[(ushort)route.ZoneId] = idx;
        }

        return new ZoneRoutingTable(map, ServerIdentity.Game1.ServerIndex);
    }

    public static IniSection? FindWorldSection(IniFile ini, int worldIndex)
    {
        ArgumentNullException.ThrowIfNull(ini);
        foreach (var (name, section) in ini.Sections)
        {
            if (IsWorldSection(name, worldIndex)) return section;
        }
        return null;
    }

    private static bool IsWorldSection(string sectionName, int worldIndex)
    {
        var trimmed = sectionName.AsSpan().Trim();
        const string prefix = "world";
        if (!trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var rest = trimmed.Slice(prefix.Length).Trim();
        return int.TryParse(rest, out var n) && n == worldIndex;
    }

    private static bool TryResolveGameServerIndex(string serverName, out ushort index)
    {
        if (string.Equals(serverName, ServerIdentity.Game1.Name, StringComparison.OrdinalIgnoreCase))
        {
            index = ServerIdentity.Game1.ServerIndex;
            return true;
        }
        if (string.Equals(serverName, ServerIdentity.Game2.Name, StringComparison.OrdinalIgnoreCase))
        {
            index = ServerIdentity.Game2.ServerIndex;
            return true;
        }
        index = 0;
        return false;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class ZoneRoutingFile
    {
        [JsonPropertyName("default")]      public ushort DefaultServerIndex { get; init; }
        [JsonPropertyName("zones")]        public Dictionary<string, ushort>? Zones { get; init; }
    }
}

public static class ZoneRouter
{
    public static ServerIdentity ResolveGameServer(ushort zoneId) =>
        (zoneId % 2) == 1 ? ServerIdentity.Game1 : ServerIdentity.Game2;
}
