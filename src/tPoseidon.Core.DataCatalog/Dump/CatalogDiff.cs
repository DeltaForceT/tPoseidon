using System.Globalization;

namespace tPoseidon.Core.DataCatalog.Dump;

public sealed record TableChange(string Table, int RowsBefore, int RowsAfter, int Added, int Removed, int Changed, IReadOnlyList<string> Examples);

public sealed record CatalogDiffReport(
    IReadOnlyList<string> TablesAdded,
    IReadOnlyList<string> TablesRemoved,
    IReadOnlyList<TableChange> TablesChanged,
    int TablesCompared)
{
    public bool Identical => TablesAdded.Count == 0 && TablesRemoved.Count == 0 && TablesChanged.Count == 0;
}

public static class CatalogDiff
{
    private const int ExampleLimit = 5;

    public static CatalogDiffReport Compare(string directoryBefore, string directoryAfter)
    {
        var before = ReadIndex(directoryBefore);
        var after = ReadIndex(directoryAfter);
        var added = after.Keys.Except(before.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var removed = before.Keys.Except(after.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var changes = new List<TableChange>();
        var compared = 0;
        foreach (var name in before.Keys.Intersect(after.Keys, StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal))
        {
            compared++;
            var change = CompareTable(name, Path.Combine(directoryBefore, before[name]), Path.Combine(directoryAfter, after[name]));
            if (change is not null) changes.Add(change);
        }
        return new CatalogDiffReport(added, removed, changes, compared);
    }

    public static string Format(CatalogDiffReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var lines = new List<string>
        {
            string.Create(CultureInfo.InvariantCulture, $"tables compared {report.TablesCompared}, added {report.TablesAdded.Count}, removed {report.TablesRemoved.Count}, changed {report.TablesChanged.Count}"),
        };
        lines.AddRange(report.TablesAdded.Select(name => $"+ table {name}"));
        lines.AddRange(report.TablesRemoved.Select(name => $"- table {name}"));
        foreach (var change in report.TablesChanged)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"~ {change.Table}: rows {change.RowsBefore} -> {change.RowsAfter}, added {change.Added}, removed {change.Removed}, changed {change.Changed}"));
            lines.AddRange(change.Examples.Select(example => $"    {example}"));
        }
        return string.Join('\n', lines);
    }

    private static Dictionary<string, string> ReadIndex(string directory)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var lines = File.ReadAllLines(Path.Combine(directory, CatalogDump.IndexFile));
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length >= 5) map[parts[0]] = parts[4];
        }
        return map;
    }

    private static TableChange? CompareTable(string name, string pathBefore, string pathAfter)
    {
        var before = File.ReadAllLines(pathBefore);
        var after = File.ReadAllLines(pathAfter);
        var headerChanged = before.Length == 0 || after.Length == 0 || before[0] != after[0];
        var rowsBefore = before.Skip(1).ToArray();
        var rowsAfter = after.Skip(1).ToArray();
        var groupsBefore = Group(rowsBefore);
        var groupsAfter = Group(rowsAfter);
        var added = 0;
        var removed = 0;
        var changed = 0;
        var examples = new List<string>();
        if (headerChanged && examples.Count < ExampleLimit) examples.Add($"columns: [{(before.Length > 0 ? before[0].Replace('\t', ',') : string.Empty)}] -> [{(after.Length > 0 ? after[0].Replace('\t', ',') : string.Empty)}]");
        foreach (var (key, listBefore) in groupsBefore)
        {
            if (!groupsAfter.TryGetValue(key, out var listAfter))
            {
                removed += listBefore.Count;
                AddExample(examples, $"removed key {key}");
                continue;
            }
            if (listBefore.Count == 1 && listAfter.Count == 1)
            {
                if (listBefore[0] != listAfter[0])
                {
                    changed++;
                    AddExample(examples, $"changed key {key}: {FirstDifference(listBefore[0], listAfter[0], before[0])}");
                }
                continue;
            }
            var leftovers = new List<string>(listAfter);
            foreach (var row in listBefore)
            {
                if (!leftovers.Remove(row)) removed++;
            }
            added += leftovers.Count;
        }
        foreach (var (key, listAfter) in groupsAfter)
        {
            if (groupsBefore.ContainsKey(key)) continue;
            added += listAfter.Count;
            AddExample(examples, $"added key {key}");
        }
        if (added == 0 && removed == 0 && changed == 0 && !headerChanged) return null;
        return new TableChange(name, rowsBefore.Length, rowsAfter.Length, added, removed, changed, examples);
    }

    private static void AddExample(List<string> examples, string text)
    {
        if (examples.Count < ExampleLimit) examples.Add(text);
    }

    private static Dictionary<string, List<string>> Group(string[] rows)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var tab = row.IndexOf('\t', StringComparison.Ordinal);
            var key = tab < 0 ? row : row[..tab];
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<string>(1);
            list.Add(row);
        }
        return groups;
    }

    private static string FirstDifference(string rowBefore, string rowAfter, string header)
    {
        var names = header.Split('\t');
        var left = rowBefore.Split('\t');
        var right = rowAfter.Split('\t');
        var diffs = new List<string>();
        for (var column = 0; column < Math.Max(left.Length, right.Length) && diffs.Count < 3; column++)
        {
            var a = column < left.Length ? left[column] : string.Empty;
            var b = column < right.Length ? right[column] : string.Empty;
            if (a != b) diffs.Add($"{(column < names.Length ? names[column] : column.ToString(CultureInfo.InvariantCulture))} '{a}' -> '{b}'");
        }
        return string.Join("; ", diffs);
    }
}
