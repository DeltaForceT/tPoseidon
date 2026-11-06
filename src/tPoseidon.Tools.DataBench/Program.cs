using System.Diagnostics;
using System.Globalization;
using tPoseidon.Core.DataCatalog;
using tPoseidon.Core.DataCatalog.Cache;
using tPoseidon.Core.DataCatalog.Fast;
using tPoseidon.Core.DataCatalog.FastTable;
using tPoseidon.Core.DataCatalog.Store;
using tPoseidon.Core.DataCatalog.Maps;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Tools.DataBench;

internal static class Program
{
    private static readonly List<string> Report = new();

    private static int Main(string[] args)
    {
        var synthetic = args.Contains("--synthetic");
        var paths = args.Where(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();
        string libconfig;
        if (synthetic)
        {
            var directory = Path.Combine(Path.GetTempPath(), $"databench-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            libconfig = SyntheticFile.Write(directory);
            Console.WriteLine($"synthetic libconfig {new FileInfo(libconfig).Length / 1048576.0:F1} MB (row counts of the KR data, filler values; shape only)");
        }
        else if (paths.Length > 0 && File.Exists(paths[0]))
        {
            libconfig = paths[0];
        }
        else
        {
            Console.Error.WriteLine("usage: DataBench <libconfig.xml|.dat> [mapDirectory] | DataBench --synthetic");
            return 2;
        }
        var mapDirectory = synthetic ? null : paths.Skip(1).FirstOrDefault();

        if (args.Contains("--startup"))
        {
            return Startup(libconfig);
        }

        Console.WriteLine("group;ms;allocatedMB;workingSetDeltaMB");
        Measure("decode+table index", () =>
        {
            var bytes = LibConfigXmlReader.DecodeFile(libconfig);
            return LibConfigTableIndex.Of(bytes).Tables.Count;
        });
        var old = new LibConfigXmlReader(libconfig);
        Measure("old: reader construct (cached decode)", () => old.DecodedXml.Length);
        Measure("old: items (4 tables)", () => old.LoadItemParam2().Count + old.LoadItemParamExtended().Count + old.LoadItemParamEx2().Count + old.LoadItemParamCm2().Count);
        Measure("old: monsters (MonsterParamEx2 + drops)", () => old.LoadMonsterParamEx2().Count + old.LoadMonItemDropInfo().Count);
        Measure("old: npc (talk + msg + character info)", () => old.LoadAllNpcTalkTables().Count + old.LoadAllNpcMsgTables().Count + old.LoadCharacterInfo().Count);
        Measure("old: skills (SkillParam2 + cards)", () => old.LoadSkillParam2().Count + old.LoadSkillCards().Count);
        Measure("old: quests", () => old.LoadAllQuestTables().Count);
        Measure("old: maps (MapInfo + pools + regions + monzones)", () => old.LoadMapInfo().Count + old.LoadAllMonsterPoolTables().Count + old.LoadAllRegionInfoTables().Count + old.LoadAllMonZoneTables().Count);
        var oldTotal = Total("old:");

        if (mapDirectory is not null && Directory.Exists(mapDirectory))
        {
            Measure("md3 (all maps, ReadMap)", () => Directory.EnumerateFiles(mapDirectory, "*.md3", SearchOption.AllDirectories).Count(file => Md3MapReader.ReadMap(file) is not null));
        }

        var decoded = LibConfigXmlReader.DecodeFile(libconfig);
        Measure("old: MapInfoEx through its nine loaders", () => old.LoadMapInfo().Count + old.LoadMapKinds().Count + old.LoadMapFiles().Count + old.LoadShopPriceRates().Count + old.LoadMapMonsterTables().Count + old.LoadTreasureMapTables().ZoneRegions.Count + old.LoadDrillTables().Zones.Count + old.LoadBossSpawnTables().Count + old.LoadPartyZoneTables().Bosses.Count);
        Measure("new: MapCatalog single row, nine projections", () =>
        {
            var maps = new MapCatalog(TableStore.Load(TableStore.Build(decoded, StoreOpener.SchemaNumber), StoreOpener.SchemaNumber), old.Overrides);
            return maps.ToMapInfo().Count + maps.ToMapKinds().Count + maps.ToMapFiles().Count + maps.ToShopPriceRates().Count + maps.ToMapMonsterTables().Count + maps.ToTreasureZoneRegions().Count + maps.ToDrillZones().Count + maps.ToBossSpawnTables().Count + maps.ToPartyZoneBosses().Count;
        });
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"databench-cache-{Guid.NewGuid():N}");
        Measure("new: items single-pass parse (4 tables, decoded bytes given)", () => ItemTables.All.Sum(table => FastItemReader.Read(decoded, ItemTables.NameOf(table)).Count));
        Measure("new: items cache build (decode, parse, write)", () => CatalogCache.LoadItems(libconfig, cacheDirectory).Items.DistinctIds);
        Measure("new: items cache hit (read binary only)", () => CatalogCache.LoadItems(libconfig, cacheDirectory).Items.DistinctIds);
        Directory.Delete(cacheDirectory, recursive: true);

        var storeDirectory = Path.Combine(Path.GetTempPath(), $"databench-store-{Guid.NewGuid():N}");
        OpenedStore? opened = null;
        Measure("new: table store build (decode, parse every table, write)", () =>
        {
            opened = StoreOpener.Open(libconfig, storeDirectory);
            return opened.Reader.Store.Tables.Count;
        });
        Measure("new: table store hit (read file, directory)", () =>
        {
            opened = StoreOpener.Open(libconfig, storeDirectory);
            return opened.Reader.Store.Tables.Count;
        });
        var fast = opened!.Reader;
        Console.WriteLine($"store file {opened.Reader.Store.SizeInBytes / 1048576.0:F1} MB");
        Measure("new: items (4 tables) from the store", () => fast.LoadItemParam2().Count + fast.LoadItemParamExtended().Count + fast.LoadItemParamEx2().Count + fast.LoadItemParamCm2().Count);
        Measure("new: monsters (MonsterParamEx2 + drops) from the store", () => fast.LoadMonsterParamEx2().Count + fast.LoadMonItemDropInfo().Count);
        Measure("new: npc (talk + msg + character info) from the store", () => fast.LoadAllNpcTalkTables().Count + fast.LoadAllNpcMsgTables().Count + fast.LoadCharacterInfo().Count);
        Measure("new: skills (SkillParam2 + cards) from the store", () => fast.LoadSkillParam2().Count + fast.LoadSkillCards().Count);
        Measure("new: quests from the store", () => fast.LoadAllQuestTables().Count);
        Measure("new: maps (MapInfo + pools + regions + monzones) from the store", () => fast.LoadMapInfo().Count + fast.LoadAllMonsterPoolTables().Count + fast.LoadAllRegionInfoTables().Count + fast.LoadAllMonZoneTables().Count);
        var newTotal = Report.Where(line => line.Contains("from the store", StringComparison.Ordinal) || line.StartsWith("new: table store hit", StringComparison.Ordinal)).Sum(line => double.Parse(line.Split(';')[1], CultureInfo.InvariantCulture));
        Console.WriteLine($"new group total ms (store hit + loaders): {newTotal:F1}");
        Directory.Delete(storeDirectory, recursive: true);

        var catalog = new LibConfigCatalog(old);
        Measure("catalog: index (rows + columns of every table)", () => catalog.Index.Tables.Count);
        Measure("catalog: items view", () => catalog.Items.DistinctIds);
        Measure("catalog: typed tables", () => catalog.Quests.Count + catalog.Monsters.Count + catalog.Maps.Count + catalog.Skills.Count + catalog.SkillCards.Count + catalog.Drops.Count + catalog.Characters.Count);

        Console.WriteLine($"old group total ms: {oldTotal:F1}");
        LookupComparison(old, catalog);
        return 0;
    }

    private static int Startup(string libconfig)
    {
        var methods = typeof(LibConfigXmlReader).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(method => method.Name.StartsWith("Load", StringComparison.Ordinal) && method.GetParameters().Length == 0)
            .Select(method => method.Name)
            .ToArray();
        var cache = Path.Combine(Path.GetTempPath(), $"databench-startup-{Guid.NewGuid():N}");
        Console.WriteLine($"{methods.Length} parameterless loaders per run (every table group the game server reads at start, plus the item, map and zone views)");
        Console.WriteLine("group;ms;allocatedMB;workingSetDeltaMB");
        Measure("startup old: decode, reader, every loader, item merge", () =>
        {
            var reader = new LibConfigXmlReader(libconfig);
            var rows = RunAll(reader, methods);
            var merged = new List<ItemParamEntry>();
            merged.AddRange(reader.LoadItemParamExtended());
            merged.AddRange(reader.LoadItemParamEx2());
            merged.AddRange(reader.LoadItemParam2());
            merged.AddRange(reader.LoadItemParamCm2());
            return rows + new InMemoryItemParamLookup(merged).Count;
        });
        Measure("startup new, first run: store build, every loader, catalog views", () => RunNew(libconfig, cache, methods));
        Measure("startup new, later runs: store hit, every loader, catalog views", () => RunNew(libconfig, cache, methods));
        Measure("startup new, later runs again", () => RunNew(libconfig, cache, methods));
        Directory.Delete(cache, recursive: true);
        return 0;
    }

    private static int RunNew(string libconfig, string cache, string[] methods)
    {
        var catalog = StoreCatalog.Open(libconfig, cache);
        var rows = RunAll(catalog.Reader, methods);
        return rows + new CatalogItemParamLookup(catalog.Items).Count + catalog.Maps.ToMapInfo().Count + catalog.Zones.ToMonZones().Count + catalog.Zones.ToMapItemZones().Count;
    }

    private static int RunAll(object reader, string[] methods)
    {
        var total = 0;
        foreach (var method in methods)
        {
            var result = reader.GetType().GetMethod(method, Type.EmptyTypes)!.Invoke(reader, null);
            total += result is System.Collections.ICollection collection ? collection.Count : 1;
        }
        return total;
    }

    private static void LookupComparison(LibConfigXmlReader old, LibConfigCatalog catalog)
    {
        var lists = new[] { old.LoadItemParam2(), old.LoadItemParamExtended(), old.LoadItemParamEx2(), old.LoadItemParamCm2() };
        var ids = catalog.Items.Resolved(ItemPrecedence.ServerDefinition).Select(record => record.Id).Take(2000).ToArray();
        if (ids.Length == 0) return;
        const int rounds = 20;

        var clock = Stopwatch.StartNew();
        var found = 0;
        for (var round = 0; round < rounds; round++)
        {
            foreach (var id in ids)
            {
                foreach (var list in lists)
                {
                    if (list.FirstOrDefault(entry => entry.Id == id) is not null) found++;
                }
            }
        }
        var scanMs = clock.Elapsed.TotalMilliseconds;

        clock.Restart();
        var hits = 0;
        for (var round = 0; round < rounds; round++)
        {
            foreach (var id in ids)
            {
                if (catalog.Items.Get(id) is not null) hits++;
            }
        }
        var directMs = clock.Elapsed.TotalMilliseconds;
        var lookups = rounds * ids.Length;
        Console.WriteLine($"lookup of {lookups} item ids: list scan over 4 tables {scanMs:F1} ms ({scanMs * 1_000_000 / lookups:F0} ns each, {found} found); catalog Get {directMs:F1} ms ({directMs * 1_000_000 / lookups:F0} ns each, {hits} found)");
    }

    private static double Total(string prefix) => Report
        .Where(line => line.StartsWith(prefix, StringComparison.Ordinal))
        .Sum(line => double.Parse(line.Split(';')[1], CultureInfo.InvariantCulture));

    private static void Measure(string name, Func<int> action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        var clock = Stopwatch.StartNew();
        var rows = action();
        clock.Stop();
        var allocatedMb = (GC.GetTotalAllocatedBytes(true) - allocated) / 1048576.0;
        var workingSetMb = (Process.GetCurrentProcess().WorkingSet64 - workingSet) / 1048576.0;
        var line = string.Create(CultureInfo.InvariantCulture, $"{name};{clock.Elapsed.TotalMilliseconds:F1};{allocatedMb:F1};{workingSetMb:F1}");
        Report.Add(line);
        Console.WriteLine($"{line};rows={rows}");
    }
}
