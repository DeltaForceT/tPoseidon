using System.Xml;
using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientWorldCheck : IClientCheck
{
    public const string CheckId = "world";
    public const string UnlistedFilesGroup = "Map files without a MapInfoEx row";

    private const string NpcTalkPrefix = "NpcTalk_";
    private const string ShopPrefix = "Shop_";
    private const string SkillShopPrefix = "SkillShopTalk_";
    private const string MonsterPrefix = "Mon_";
    private const string MapFileFilter = "*.md3";
    private const int NoRegion = 0;

    private readonly ClientPatchCatalog _catalog;
    private readonly IReadOnlyList<LibConfigRowFix> _rowFixes;

    public ClientWorldCheck(ClientPatchCatalog catalog, IReadOnlyList<LibConfigRowFix> rowFixes)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rowFixes);
        _catalog = catalog;
        _rowFixes = rowFixes;
    }

    public string Id => CheckId;

    public bool ReadsWorldData => true;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var libconfig = context.Plan.LibConfig;
        if (!File.Exists(libconfig) || !Directory.Exists(context.MapFolder))
        {
            return [];
        }

        WorldTables tables;
        try
        {
            tables = WorldTables.Read(libconfig);
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or IOException or ArgumentException or UnauthorizedAccessException)
        {
            return [ScanFinding.Problem(Id, $"{Path.GetFileName(libconfig)} could not be read, so the world data was not checked: {ex.Message}")];
        }

        var scan = new WorldScan(context, tables, _catalog, _rowFixes);
        scan.Run();
        if (scan.Findings.Count == 0)
        {
            scan.Findings.Add(ScanFinding.Pass(Id, $"{tables.Maps.Count} maps: files, portals, NPCs, shops and spawn regions all line up."));
        }

        return scan.Findings;
    }

    private sealed record WorldTables(
        IReadOnlyList<MapInfoEntry> Maps,
        InMemoryNpcTalkLookup Talk,
        InMemoryShopZoneLookup Shops,
        InMemorySkillShopZoneLookup SkillShops,
        IReadOnlyDictionary<int, CharacterInfoEntry> Characters,
        ILookup<string, MonZoneEntry> Monsters)
    {
        public static WorldTables Read(string libconfig)
        {
            var reader = new LibConfigXmlReader(libconfig);
            return new WorldTables(
                reader.LoadMapInfo().GroupBy(map => map.Id).Select(group => group.First()).OrderBy(map => map.Id).ToList(),
                new InMemoryNpcTalkLookup(reader.LoadAllNpcTalkTables()),
                new InMemoryShopZoneLookup(reader.LoadAllShopZoneTables()),
                new InMemorySkillShopZoneLookup(reader.LoadAllSkillShopZoneTables()),
                reader.LoadCharacterInfo().GroupBy(entry => entry.Id).ToDictionary(group => group.Key, group => group.First()),
                reader.LoadAllMonZoneTables().ToLookup(entry => entry.MapName, StringComparer.Ordinal));
        }
    }

    private sealed class WorldScan
    {
        private readonly ClientScanContext _context;
        private readonly WorldTables _tables;
        private readonly ClientPatchCatalog _catalog;
        private readonly IReadOnlyList<LibConfigRowFix> _rowFixes;
        private readonly Dictionary<ushort, Md3Map> _maps = [];
        private readonly Dictionary<ushort, MapInfoEntry> _byId;

        public WorldScan(ClientScanContext context, WorldTables tables, ClientPatchCatalog catalog, IReadOnlyList<LibConfigRowFix> rowFixes)
        {
            _context = context;
            _tables = tables;
            _catalog = catalog;
            _rowFixes = rowFixes;
            _byId = tables.Maps.ToDictionary(map => map.Id);
        }

        public List<ScanFinding> Findings { get; } = [];

        public void Run()
        {
            foreach (var map in _tables.Maps)
            {
                LoadMap(map);
            }

            foreach (var map in _tables.Maps.Where(map => _maps.ContainsKey(map.Id)))
            {
                CheckPortals(map);
                CheckNpcs(map);
                CheckShops(map);
                CheckSpawnRegions(map);
            }

            CheckUnlistedFiles();
        }

        private static string Label(MapInfoEntry map) => $"Map {map.Id} {map.Name}".TrimEnd();

        private void Add(MapInfoEntry map, ScanStatus status, string message, string? fixId = null) =>
            Findings.Add(new ScanFinding(CheckId, status, message, fixId, Label(map)));

        private void LoadMap(MapInfoEntry map)
        {
            if (map.Md3FileName.Length == 0)
            {
                return;
            }

            var located = ClientFiles.Locate(ClientFiles.Combine(_context.MapFolder, map.Md3FileName));
            if (located is null)
            {
                Add(map, ScanStatus.Problem, $"The map file {map.Md3FileName} is missing from the map folder.");
                return;
            }

            if (Md3MapReader.ReadMap(located) is not { } md3)
            {
                Add(map, ScanStatus.Problem, $"The map file {map.Md3FileName} could not be read.");
                return;
            }

            _maps[map.Id] = md3;
        }

        private void CheckPortals(MapInfoEntry map)
        {
            foreach (var range in _maps[map.Id].Ranges.Where(range => range.Destination > 0))
            {
                if (range.Destination > ushort.MaxValue || !_byId.TryGetValue((ushort)range.Destination, out var destination))
                {
                    Add(map, ScanStatus.Problem, $"Portal {range.Id} leads to map {range.Destination}, which is not in the map table.");
                    continue;
                }

                if (!_maps.TryGetValue(destination.Id, out var arrival))
                {
                    Add(map, ScanStatus.Problem, $"Portal {range.Id} leads to map {destination.Id}, whose map file is missing.");
                    continue;
                }

                var arrivals = arrival.Points.Where(point => point.Type == JsonPortalLookup.ArrivalPointType).ToList();
                if (arrivals.Count > 0)
                {
                    continue;
                }

                var patch = _catalog.FindArrival(destination.Md3FileName, map.Id);
                Add(map, ScanStatus.Problem, $"Portal {range.Id} leads to map {destination.Id}, which has no arrival point at all; players would land at {PortalArrivalRule.FallbackX},{PortalArrivalRule.FallbackY}.{NoFix(patch is null, "no source gives the arrival position")}", patch is null ? null : Md3PointsFix.FixId);
            }
        }

        private void CheckNpcs(MapInfoEntry map)
        {
            if (!map.NpcTable.StartsWith(NpcTalkPrefix, StringComparison.Ordinal))
            {
                return;
            }

            foreach (var row in _tables.Talk.GetAllByMap(map.NpcTable[NpcTalkPrefix.Length..]))
            {
                if (!NpcManager.IsValidChrType(row.Type))
                {
                    Add(map, ScanStatus.Warning, $"NPC at point {row.PosId} has the character type {row.Type}, which the server does not place.");
                    continue;
                }

                CheckNpcRow(map, "NPC", row.Type, row.PosId, MapPointTypes.Npc);
            }
        }

        private void CheckShops(MapInfoEntry map)
        {
            if (map.ShopTable.StartsWith(ShopPrefix, StringComparison.Ordinal))
            {
                foreach (var row in _tables.Shops.GetByMap(map.ShopTable[ShopPrefix.Length..]))
                {
                    CheckNpcRow(map, "Shop NPC", row.NpcType, row.PosId, MapPointTypes.Shop);
                }
            }

            if (map.SkillShopTable.StartsWith(SkillShopPrefix, StringComparison.Ordinal))
            {
                foreach (var row in _tables.SkillShops.GetByMap(map.SkillShopTable[SkillShopPrefix.Length..]))
                {
                    CheckNpcRow(map, "Skill shop NPC", row.NpcType, row.PosId, MapPointTypes.SkillShop);
                }
            }
        }

        private void CheckNpcRow(MapInfoEntry map, string kind, int type, int posId, int pointType)
        {
            if (!_maps[map.Id].Points.Any(point => point.Type == pointType && point.Id == posId))
            {
                var patch = _catalog.FindPoint(map.Md3FileName, pointType, posId);
                Add(map, ScanStatus.Problem, $"{kind} {type} has no point {posId} (type {pointType}) in the map file; the server would put it at {NpcPoint.Origin().X:0},{NpcPoint.Origin().Y:0}.{NoFix(patch is null, "no source gives the point position")}", patch is null ? null : Md3PointsFix.FixId);
            }

            CheckArt(map, kind, type);
        }

        private void CheckArt(MapInfoEntry map, string kind, int type)
        {
            if (!_tables.Characters.TryGetValue(type, out var character))
            {
                Add(map, ScanStatus.Warning, $"{kind} type {type} has no CharacterInfo row, so the client has no picture for it.");
                return;
            }

            if (character.FileName.Length == 0)
            {
                return;
            }

            var found = ClientFiles.Exists(ClientFiles.Combine(_context.ClientFolder, character.FileName))
                || ClientFiles.Exists(ClientFiles.Combine(_context.DataFolder, character.FileName));
            if (!found)
            {
                Add(map, ScanStatus.Warning, $"{kind} type {type} ({character.DisplayName}) has no picture file {character.FileName}.");
            }
        }

        private void CheckSpawnRegions(MapInfoEntry map)
        {
            if (!map.MonsterTable.StartsWith(MonsterPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var regions = _tables.Monsters[map.MonsterTable[MonsterPrefix.Length..]]
                .Where(row => row.Region != NoRegion)
                .Select(row => row.Region)
                .Distinct()
                .ToList();
            if (regions.Count == 0)
            {
                return;
            }

            if (Md3ZoneGeometryReader.Read(Path.Combine(_context.MapFolder, map.Md3FileName)) is not { } geometry)
            {
                return;
            }

            var gaps = regions
                .Where(region => !geometry.Points.Any(point => point.Type == ZoneGeometry.PointTypeRegionSpawn && point.Id == region))
                .Order()
                .ToList();
            if (gaps.Count > 0)
            {
                var noun = gaps.Count == 1 ? "region" : "regions";
                Findings.Add(new ScanFinding(CheckId, ScanStatus.Info, $"Monsters in {noun} {string.Join(", ", gaps)} have no spawn point of their own in the map file, so they spawn at the map's general spawn points. Nothing to fix.", null, Label(map)));
            }
        }

        private void CheckUnlistedFiles()
        {
            var listed = _tables.Maps.Select(map => map.Md3FileName).Where(name => name.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unused = new List<string>();
            foreach (var file in Directory.EnumerateFiles(_context.MapFolder, MapFileFilter).Select(Path.GetFileName).Order(StringComparer.OrdinalIgnoreCase))
            {
                if (listed.Contains(file!))
                {
                    continue;
                }

                if (_rowFixes.FirstOrDefault(fix => fix.MapFiles.Contains(file!)) is { } rowFix)
                {
                    Findings.Add(new ScanFinding(CheckId, ScanStatus.Warning, $"{file} is in the map folder but no MapInfoEx row uses it, so the server never loads it. The fix '{rowFix.Title}' adds its row.", rowFix.Id, UnlistedFilesGroup));
                    continue;
                }

                unused.Add(file!);
            }

            if (unused.Count > 0)
            {
                var noun = unused.Count == 1 ? "map file is" : "map files are";
                Findings.Add(new ScanFinding(CheckId, ScanStatus.Info, $"{unused.Count} {noun} unused by the game data (no MapInfoEx row, and no portal, teleport or quest leads to it), so the server never loads {(unused.Count == 1 ? "it" : "them")} and nothing needs fixing: {string.Join(", ", unused)}.", null, UnlistedFilesGroup));
            }
        }

        private static string NoFix(bool reportOnly, string reason) => reportOnly ? $" Report only: {reason}." : "";
    }
}
