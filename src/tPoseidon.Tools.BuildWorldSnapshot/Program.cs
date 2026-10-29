using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;
using tPoseidon.Core.WorldSnapshot;

const string LogTag = "[mt-build-world-snapshot]";

string variant = "en2010";
string? dataDir = null;
string? outputPath = null;
string? libconfigOverride = null;
string? md3DirOverride = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--variant" or "-v":
            if (++i >= args.Length) return Usage("missing value for --variant");
            variant = args[i];
            break;
        case "--data-dir" or "-d":
            if (++i >= args.Length) return Usage("missing value for --data-dir");
            dataDir = args[i];
            break;
        case "--output" or "-o":
            if (++i >= args.Length) return Usage("missing value for --output");
            outputPath = args[i];
            break;
        case "--libconfig" or "-l":
            if (++i >= args.Length) return Usage("missing value for --libconfig");
            libconfigOverride = args[i];
            break;
        case "--md3-dir" or "-m":
            if (++i >= args.Length) return Usage("missing value for --md3-dir");
            md3DirOverride = args[i];
            break;
        case "--help" or "-h":
            return Usage(null);
        default:
            return Usage($"unknown argument '{args[i]}'");
    }
}

if (variant is not ("en2010" or "kr2014" or "en" or "kr"))
{
    return Usage($"--variant must be 'en2010' or 'kr2014' (got '{variant}')");
}
variant = variant is "en" or "en2010" ? "en2010" : "kr2014";

dataDir ??= tPoseidon.Core.Common.RepoRoot.DataDir;
outputPath ??= Path.Combine(dataDir, "cache", $"{variant}-world.snapshot");

Console.WriteLine($"{LogTag} variant={variant}");
Console.WriteLine($"{LogTag} data dir: {dataDir}");
Console.WriteLine($"{LogTag} output:   {outputPath}");

var warnings = new List<string>();
void Warn(string msg) { warnings.Add(msg); Console.Error.WriteLine($"{LogTag} WARN: {msg}"); }

var sourceHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
void Hash(string label, string path)
{
    if (!File.Exists(path)) return;
    sourceHashes[label] = WorldSnapshotIo.HashFile(path);
}

var mapInfoPath = Path.Combine(dataDir, "map-info.json");
var portalsPath = Path.Combine(dataDir, "portals.json");
var monsterSpawnsPath = Path.Combine(dataDir, "monster-spawns.json");
var zonePoolsPath = Path.Combine(dataDir, "zone-mob-pools.json");

if (string.IsNullOrWhiteSpace(libconfigOverride))
{
    return Usage("missing --libconfig <path>: pass the client libconfig.dat (decrypted in-process) or a pre-decrypted XML for the chosen variant");
}
var libconfigPath = Path.GetFullPath(libconfigOverride);
if (!File.Exists(libconfigPath))
{
    Console.Error.WriteLine($"{LogTag} ERROR: libconfig not found at {libconfigPath}");
    return 2;
}

if (string.IsNullOrWhiteSpace(md3DirOverride))
{
    return Usage("missing --md3-dir <path>: pass the client's data/map directory containing the .md3 files");
}
var md3Dir = Path.GetFullPath(md3DirOverride);
if (!Directory.Exists(md3Dir))
{
    Console.Error.WriteLine($"{LogTag} ERROR: md3 directory not found at {md3Dir}");
    return 2;
}

Hash("map-info.json", mapInfoPath);
Hash("portals.json", portalsPath);
Hash("monster-spawns.json", monsterSpawnsPath);
Hash("zone-mob-pools.json", zonePoolsPath);
Hash(Path.GetFileName(libconfigPath), libconfigPath);

Console.WriteLine($"{LogTag} libconfig: {libconfigPath}");
Console.WriteLine($"{LogTag} md3 dir:   {md3Dir}");

IMapInfoLookup mapInfoLookup;
try
{
    mapInfoLookup = JsonMapInfoLookup.LoadFromFile(mapInfoPath);
    Console.WriteLine($"{LogTag} map-info: {mapInfoLookup.Count} entries");
}
catch (Exception ex)
{
    Warn($"map-info.json load failed ({ex.GetType().Name}: {ex.Message})");
    return 2;
}

IPortalLookup? portalLookup = null;
if (File.Exists(portalsPath))
{
    try
    {
        portalLookup = JsonPortalLookup.LoadFromPortalsJson(portalsPath);
        Console.WriteLine($"{LogTag} portals.json: {portalLookup.TotalPortals} portals across {portalLookup.MapCount} maps");
    }
    catch (Exception ex)
    {
        Warn($"portals.json load failed ({ex.GetType().Name}: {ex.Message})");
    }
}

IMonsterParamLookup monsterCatalog;
IMonItemDropInfoLookup dropCatalog;
INpcTalkLookup npcTalkCatalog;
try
{
    var reader = new LibConfigXmlReader(libconfigPath);

    var monsterEntries = reader.LoadMonsterParamEx2();
    monsterCatalog = new InMemoryMonsterParamLookup(monsterEntries);
    Console.WriteLine($"{LogTag} monsters libconfig: {monsterCatalog.Count} entries");

    var dropEntries = reader.LoadMonItemDropInfo();
    dropCatalog = new InMemoryMonItemDropInfoLookup(dropEntries);
    Console.WriteLine($"{LogTag} drops libconfig: {dropCatalog.Count} entries");

    npcTalkCatalog = new InMemoryNpcTalkLookup(reader.LoadAllNpcTalkTables());
    Console.WriteLine($"{LogTag} NpcTalk libconfig: {npcTalkCatalog.Count} entries");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{LogTag} ERROR: libconfig parse failed ({ex.GetType().Name}: {ex.Message})");
    return 2;
}

var npcPointProvider = Md3PointCatalog.Load(
    mapInfoLookup.All.Where(map => map.NpcTable.Length > 0),
    md3Dir,
    MapPointTypes.Npc);
Console.WriteLine($"{LogTag} npc-points: {npcPointProvider.Count} points read from the map files");

IMonsterSpawnProvider monsterSpawnProvider;
try
{
    monsterSpawnProvider = JsonMonsterSpawnProvider.LoadFromFile(monsterSpawnsPath);
    Console.WriteLine($"{LogTag} monster-spawns loaded");
}
catch (Exception ex)
{
    Warn($"monster-spawns load failed ({ex.GetType().Name}: {ex.Message})");
    monsterSpawnProvider = new JsonMonsterSpawnProvider(new Dictionary<ushort, IReadOnlyList<MonsterSpawnPoint>>());
}

IZoneMonsterPoolLookup zonePools;
try
{
    zonePools = JsonZoneMonsterPoolLookup.LoadFromFile(zonePoolsPath);
    Console.WriteLine($"{LogTag} zone-mob-pools loaded");
}
catch (Exception ex)
{
    Warn($"zone-mob-pools load failed ({ex.GetType().Name}: {ex.Message})");
    zonePools = new JsonZoneMonsterPoolLookup(new Dictionary<ushort, ZoneMonsterPool>());
}

var idAllocator = new IdAllocator();
var worldEntities = new WorldEntityStore();

foreach (var (_, pinnedId) in NpcManager.CoralTownPinnedIds())
{
    idAllocator.Reserve(pinnedId);
}

var npcManager = new NpcManager(
    npcTalkCatalog, npcPointProvider, worldEntities, idAllocator,
    log: msg => Console.WriteLine($"{LogTag} {msg}"),
    pinnedEntityIds: NpcManager.CoralTownPinnedIds());

var monsterManager = new tPoseidon.Core.Game.Monsters.MonsterManager(monsterCatalog, idAllocator, dropCatalog);

var zonePoolOverridesPath = Path.Combine(dataDir, "zone_pool_overrides.json");
var spawnPointOverridesPath = Path.Combine(dataDir, "spawn_point_overrides.json");
var zonePoolOverrides = new ZonePoolOverridesProvider(zonePoolOverridesPath, watchForChanges: false);
var spawnPointOverrides = new SpawnPointOverridesProvider(spawnPointOverridesPath, watchForChanges: false);

var mapCreator = new MapCreator(portalLookup, () => md3Dir);
var npcCreator = new NpcCreator(npcManager, msg => Console.WriteLine($"{LogTag} {msg}"));
var monsterCreator = new MonsterCreator(monsterManager, monsterSpawnProvider, zonePools, zonePoolOverrides, spawnPointOverrides, () => 1);

var zoneManager = new ZoneManager(
    mapInfoLookup,
    new IZoneCreator[] { mapCreator },
    new IZoneEntityCreator[] { npcCreator, monsterCreator },
    initializer: null,
    log: msg => Console.WriteLine($"{LogTag} [zone] {msg}"));

var sw = System.Diagnostics.Stopwatch.StartNew();
zoneManager.BuildWorld();
zoneManager.SpawnAll();
sw.Stop();
Console.WriteLine($"{LogTag} build complete in {sw.ElapsedMilliseconds} ms — {worldEntities.Count} NPCs + {monsterManager.Count} monsters across {zoneManager.Count} zones");

var zoneSummaries = zoneManager.All
    .OrderBy(z => z.MapId)
    .Select(z => new ZoneSummary
    {
        MapId = z.MapId,
        Name = z.Name,
        Md3FileName = z.Md3FileName,
        NpcSpawnCount = z.NpcSpawnCount,
        MonsterSpawnCount = z.MonsterSpawnCount,
        Portals = z.Portals,
    })
    .ToArray();

var zoneGeometry = new Dictionary<ushort, ZoneGeometry>();
foreach (var zone in zoneSummaries)
{
    var geometry = Md3ZoneGeometryReader.Read(Path.Combine(md3Dir, zone.Md3FileName));
    if (geometry is not null) zoneGeometry[zone.MapId] = geometry;
}
Console.WriteLine($"{LogTag} zone geometry read for {zoneGeometry.Count} of {zoneSummaries.Length} zones");

System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
var clientRoot = Path.GetFullPath(Path.Combine(md3Dir, "..", ".."));
var combatInfos = CharacterCombatInfo.Build(
    new LibConfigXmlReader(libconfigPath).LoadCharacterInfo(),
    clientRoot,
    System.Text.Encoding.GetEncoding(NoriAnimationReader.NameCodePage));
Console.WriteLine($"{LogTag} combat info for {combatInfos.Count} character types ({combatInfos.Count(c => c.AttackMs > 0)} with attack timing) from {clientRoot}");

var snapshot = WorldSnapshotBuilder.Build(
    variant,
    zoneSummaries,
    worldEntities.All(),
    monsterManager.All.ToArray(),
    idAllocator.NextIdHighWaterMark,
    sourceHashes,
    warnings,
    zoneGeometry,
    combatInfos);

WorldSnapshotIo.Save(outputPath, snapshot);

var fi = new FileInfo(outputPath);
Console.WriteLine($"{LogTag} snapshot written: {outputPath} ({fi.Length:N0} bytes)");
return 0;

static int Usage(string? error)
{
    if (error is not null)
    {
        Console.Error.WriteLine($"[mt-build-world-snapshot] ERROR: {error}");
    }
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  mt-build-world-snapshot --variant en2010|kr2014");
    Console.Error.WriteLine("                          --libconfig <path-to-libconfig.dat-or-xml>");
    Console.Error.WriteLine("                          --md3-dir <path-to-client-data/map>");
    Console.Error.WriteLine("                          [--data-dir <dir>] [--output <path>]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("--libconfig REQUIRED: client libconfig.dat (decrypted in-process) OR a pre-decrypted XML");
    Console.Error.WriteLine("--md3-dir   REQUIRED: path to the client install's data/map directory containing .md3 files");
    Console.Error.WriteLine("--data-dir  defaults to ../../../../../data (walk up from tool's bin output)");
    Console.Error.WriteLine("--output    defaults to <data-dir>/cache/<variant>-world.snapshot");
    return error is null ? 0 : 1;
}
