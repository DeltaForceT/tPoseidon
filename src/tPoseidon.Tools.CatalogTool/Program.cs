using System.Globalization;
using tPoseidon.Core.DataCatalog;
using tPoseidon.Core.DataCatalog.Dump;
using tPoseidon.Core.DataCatalog.Rules;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Tools.CatalogTool;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: CatalogTool <index|duplicates|clashes> <libconfig> | dump <libconfig> <directory> | diff <dumpBefore> <dumpAfter>");
            return 2;
        }
        if (args[0] == "diff" && args.Length >= 3)
        {
            var report = CatalogDiff.Compare(args[1], args[2]);
            Console.WriteLine(CatalogDiff.Format(report));
            return report.Identical ? 0 : 1;
        }
        if (args[0] == "dump" && args.Length >= 3)
        {
            var summary = CatalogDump.Write(LibConfigXmlReader.DecodeFile(args[1]), CatalogVersion.Of(args[1]), args[2]);
            Console.WriteLine($"dumped {summary.Tables} tables, {summary.Rows} rows to {summary.Directory} (content {summary.FileSet[..12]})");
            return 0;
        }
        var catalog = StoreCatalog.Open(args[1]);
        switch (args[0])
        {
            case "index":
                foreach (var table in catalog.Index.Tables)
                {
                    Console.WriteLine(string.Join('\t', table.Name, table.Family, table.Occurrences.ToString(CultureInfo.InvariantCulture), table.RowCount.ToString(CultureInfo.InvariantCulture), string.Join(',', table.Columns)));
                }
                return 0;
            case "duplicates":
                Console.WriteLine($"spans {catalog.Index.SpanCount}, distinct names {catalog.Index.Tables.Count}");
                foreach (var table in catalog.Index.Duplicated) Console.WriteLine($"{table.Name}\t{table.Occurrences}");
                return 0;
            case "families":
                Families(catalog);
                return 0;
            case "clashes":
                Clashes(catalog);
                return 0;
            default:
                Console.Error.WriteLine($"unknown command {args[0]}");
                return 2;
        }
    }

    private static void Families(StoreCatalog catalog)
    {
        foreach (var group in catalog.Index.Tables.GroupBy(TableRules.KindOf).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"{group.Key}: {group.Count()} tables, {group.Sum(table => table.RowCount)} rows");
        }
        var old = catalog.Reader;
        var monZones = old.LoadAllMonZoneTables();
        var pools = catalog.Zones.ToMonZones();
        Console.WriteLine($"Mon_ prefix loader rows {monZones.Count}, pool-table rows {pools.Count}, foreign rows {monZones.Count - pools.Count}");
        var mapItems = old.LoadAllMapItemZoneTables();
        var zoneItems = catalog.Zones.ToMapItemZones();
        Console.WriteLine($"MapItem_ prefix loader rows {mapItems.Count}, zone-table rows {zoneItems.Count}, foreign rows {mapItems.Count - zoneItems.Count}");
        var actions = old.LoadAllEsActionTables();
        var skillActions = catalog.Zones.ToSkillActions();
        Console.WriteLine($"ESAction_ prefix loader rows {actions.Count}, skill-action rows {skillActions.Count}, foreign rows {actions.Count - skillActions.Count}");
    }

    private static void Clashes(StoreCatalog catalog)
    {
        var items = catalog.Items;
        foreach (var table in ItemTables.All) Console.WriteLine($"{ItemTables.NameOf(table)}: {items.Rows(table).Count} rows, {items.Count(table)} distinct ids");
        Console.WriteLine($"distinct ids overall {items.DistinctIds}, ids in more than one table {items.Clashes.Count}");
        foreach (var group in items.Clashes.GroupBy(clash => string.Join('+', clash.Tables.Select(ItemTables.NameOf))))
        {
            Console.WriteLine($"{group.Key}: {group.Count()}");
        }
        var differ = 0;
        var sameFields = 0;
        foreach (var clash in items.Clashes)
        {
            var server = items.Get(clash.Id, ItemPrecedence.ServerDefinition)!;
            var named = items.GetNamed(clash.Id);
            var disagree = named is null || named.Source != server.Source;
            if (disagree) differ++;
            else sameFields++;
        }
        Console.WriteLine($"clashes where the server pick and the display-name pick are different tables: {differ}; same table: {sameFields}");
        foreach (var clash in items.Clashes.Take(25))
        {
            var server = items.Get(clash.Id, ItemPrecedence.ServerDefinition)!;
            var named = items.GetNamed(clash.Id);
            var rows = string.Join(" | ", clash.Tables.Select(table => $"{ItemTables.NameOf(table)}:{items.Find(table, clash.Id)!.Name}/T{items.Find(table, clash.Id)!.Type}"));
            Console.WriteLine($"{clash.Id}\tserver={ItemTables.NameOf(server.Source)}\tdisplay={(named is null ? "none" : ItemTables.NameOf(named.Source))}\t{rows}");
        }
    }
}
