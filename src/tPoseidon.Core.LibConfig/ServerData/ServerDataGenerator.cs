using System.Globalization;
using System.Text.Json;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.LibConfig.ServerData;

public sealed record ServerDataSummary(int Maps, int PortalMaps, int Portals, int PoolMaps, int PoolRows);

public static class ServerDataGenerator
{
    public const string MapInfoFile = "map-info.json";
    public const string PortalsFile = "portals.json";
    public const string ZonePoolsFile = "zone-mob-pools.json";
    public const string MonsterSpawnsFile = "monster-spawns.json";

    private const int PortalArrivalPointType = 1;
    private const int PortalEntranceRangeType = 0;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };

    public static ServerDataSummary Generate(string libconfigPath, string mapDirectory, string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(libconfigPath);
        ArgumentException.ThrowIfNullOrEmpty(mapDirectory);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);

        var reader = new LibConfigXmlReader(libconfigPath);
        var maps = reader.LoadMapInfo().GroupBy(map => map.Id).Select(group => group.First()).OrderBy(map => map.Id).ToList();
        var pools = reader.LoadAllMonsterPoolTables();
        var md3ByMap = new Dictionary<ushort, Md3Map>();
        foreach (var map in maps)
        {
            if (map.Md3FileName.Length == 0) continue;
            var located = ClientFiles.Locate(ClientFiles.Combine(mapDirectory, map.Md3FileName));
            if (located is not null && Md3MapReader.ReadMap(located) is { } md3)
            {
                md3ByMap[map.Id] = md3;
            }
        }

        Directory.CreateDirectory(outputDirectory);
        WriteMapInfo(Path.Combine(outputDirectory, MapInfoFile), maps);
        var (portalMaps, portals) = WritePortals(Path.Combine(outputDirectory, PortalsFile), maps, md3ByMap);
        var (poolMaps, poolRows) = WriteZonePools(Path.Combine(outputDirectory, ZonePoolsFile), maps, pools);
        WriteEmptyObject(Path.Combine(outputDirectory, MonsterSpawnsFile));
        return new ServerDataSummary(maps.Count, portalMaps, portals, poolMaps, poolRows);
    }

    private static void WriteMapInfo(string path, IReadOnlyList<MapInfoEntry> maps)
    {
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream, WriterOptions);
        json.WriteStartObject();
        foreach (var map in maps)
        {
            json.WriteStartObject(Key(map.Id));
            json.WriteNumber("BossMonCheckTime", map.BossMonCheckTime);
            json.WriteNumber("BossMonsterCnt", map.BossMonsterCnt);
            json.WriteString("BossMonsterTable", map.BossMonsterTable);
            json.WriteString("ItemTable", map.ItemTable);
            json.WriteNumber("MaxDepth", map.MaxDepth);
            json.WriteString("Md3FileName", map.Md3FileName);
            json.WriteNumber("MonsterAvrNum", map.MonsterAvrNum);
            json.WriteString("MonsterTable", map.MonsterTable);
            json.WriteString("Name", map.Name);
            json.WriteString("NpcTable", map.NpcTable);
            json.WriteString("ShopTable", map.ShopTable);
            json.WriteString("SkillShopTable", map.SkillShopTable);
            json.WriteEndObject();
        }

        json.WriteEndObject();
    }

    private static (int Maps, int Portals) WritePortals(string path, IReadOnlyList<MapInfoEntry> maps, Dictionary<ushort, Md3Map> md3ByMap)
    {
        var mapCount = 0;
        var portalCount = 0;
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream, WriterOptions);
        json.WriteStartObject();
        foreach (var map in maps)
        {
            if (!md3ByMap.TryGetValue(map.Id, out var md3)) continue;
            var entrances = md3.Ranges.Where(range => range.Type == PortalEntranceRangeType && range.Destination > 0).ToList();
            var arrivals = md3.Points.Where(point => point.Type == PortalArrivalPointType && point.MapId > 0).ToList();
            if (entrances.Count == 0 && arrivals.Count == 0) continue;

            mapCount++;
            portalCount += entrances.Count;
            json.WriteStartObject(Key(map.Id));
            json.WriteStartArray("Arrivals");
            foreach (var arrival in arrivals)
            {
                json.WriteStartObject();
                json.WriteNumber("FromMapId", arrival.MapId);
                json.WriteNumber("X", arrival.X);
                json.WriteNumber("Y", arrival.Y);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteStartArray("Entrances");
            foreach (var entrance in entrances)
            {
                json.WriteStartObject();
                json.WriteNumber("DestMapId", entrance.Destination);
                json.WriteNumber("Id", entrance.Id);
                json.WriteNumber("X1", entrance.X1);
                json.WriteNumber("X2", entrance.X2);
                json.WriteNumber("Y1", entrance.Y1);
                json.WriteNumber("Y2", entrance.Y2);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteString("MapName", map.Name);
            json.WriteString("Md3FileName", map.Md3FileName);
            json.WriteEndObject();
        }

        json.WriteEndObject();
        return (mapCount, portalCount);
    }

    private static (int Maps, int Rows) WriteZonePools(string path, IReadOnlyList<MapInfoEntry> maps, IReadOnlyDictionary<string, IReadOnlyList<MonsterPoolRow>> pools)
    {
        var mapCount = 0;
        var rowCount = 0;
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream, WriterOptions);
        json.WriteStartObject();
        foreach (var map in maps)
        {
            if (map.MonsterTable.Length == 0 || !pools.TryGetValue(map.MonsterTable, out var rows) || rows.Count == 0) continue;
            mapCount++;
            json.WriteStartObject(Key(map.Id));
            json.WriteString("MapName", map.Name);
            json.WriteString("MonsterTable", map.MonsterTable);
            json.WriteNumber("MonsterAvrNum", map.MonsterAvrNum);
            json.WriteStartArray("Pool");
            foreach (var row in rows)
            {
                json.WriteStartObject();
                json.WriteNumber("MonsterID", row.MonsterId);
                json.WriteNumber("Avr", row.Avr);
                json.WriteNumber("Max", row.Max);
                json.WriteNumber("Ratio", row.Ratio);
                json.WriteNumber("Region", row.Region);
                json.WriteEndObject();
                rowCount++;
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndObject();
        return (mapCount, rowCount);
    }

    private static void WriteEmptyObject(string path)
    {
        using var stream = File.Create(path);
        using var json = new Utf8JsonWriter(stream, WriterOptions);
        json.WriteStartObject();
        json.WriteEndObject();
    }

    private static string Key(ushort id) => id.ToString(CultureInfo.InvariantCulture);
}
