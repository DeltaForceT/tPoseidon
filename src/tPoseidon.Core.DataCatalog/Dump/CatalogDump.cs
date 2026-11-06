using System.Globalization;
using System.Text;
using System.Text.Json;
using tPoseidon.Core.DataCatalog.FastTable;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.Dump;

public sealed record DumpSummary(string Directory, int Tables, long Rows, string FileSet);

public static class CatalogDump
{
    public const string IndexFile = "index.tsv";
    public const string VersionFile = "version.json";
    public const string TablesDirectory = "tables";
    private static readonly JsonSerializerOptions VersionOptions = new() { WriteIndented = true };

    public const string FileMapFile = "files.tsv";

    public static DumpSummary Write(byte[] xml, CatalogVersion version, string directory)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var tablesDirectory = Path.Combine(directory, TablesDirectory);
        System.IO.Directory.CreateDirectory(tablesDirectory);
        foreach (var stale in System.IO.Directory.EnumerateFiles(tablesDirectory, "*.tsv")) File.Delete(stale);

        var spans = LibConfigTableIndex.Of(xml).Tables;
        var occurrences = spans.GroupBy(span => span.Name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = new StringBuilder("Name\tFamily\tOccurrences\tRows\tFile\tReaders\tColumns\n");
        var written = new HashSet<string>(StringComparer.Ordinal);
        long totalRows = 0;
        foreach (var span in spans)
        {
            if (!written.Add(span.Name)) continue;
            var fileName = UniqueFileName(span.Name, usedNames);
            var (rows, columns) = WriteTable(xml, span, Path.Combine(tablesDirectory, fileName));
            totalRows += rows;
            index.Append(span.Name).Append('\t')
                .Append(TableFamilies.Of(span.Name)).Append('\t')
                .Append(occurrences[span.Name].ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(rows.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(TablesDirectory).Append('/').Append(fileName).Append('\t')
                .Append(string.Join(',', TableReaders.Of(span.Name))).Append('\t')
                .Append(string.Join(',', columns)).Append('\n');
        }
        File.WriteAllText(Path.Combine(directory, IndexFile), index.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, VersionFile), JsonSerializer.Serialize(version, VersionOptions), new UTF8Encoding(false));
        return new DumpSummary(directory, written.Count, totalRows, version.Sha256);
    }

    private static string UniqueFileName(string tableName, HashSet<string> used)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(tableName.Select(character => Array.IndexOf(invalid, character) >= 0 ? '_' : character).ToArray());
        var candidate = safe;
        var counter = 1;
        while (!used.Add(candidate)) candidate = string.Create(CultureInfo.InvariantCulture, $"{safe}~{counter++}");
        return candidate + ".tsv";
    }

    private static (int Rows, IReadOnlyList<string> Columns) WriteTable(byte[] xml, LibConfigTableSpan span, string path)
    {
        var rows = new List<string[]>();
        IReadOnlyList<string> names = Array.Empty<string>();
        FastTableReader.Read(xml, span, row =>
        {
            var values = new string[row.SlotCount];
            for (var slot = 0; slot < values.Length; slot++) values[slot] = Escape(row.SlotValue(slot));
            rows.Add(values);
            names = row.SlotNames;
        });
        using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.Write(string.Join('\t', names));
        writer.Write('\n');
        foreach (var values in rows)
        {
            for (var column = 0; column < names.Count; column++)
            {
                if (column > 0) writer.Write('\t');
                if (column < values.Length) writer.Write(values[column]);
            }
            writer.Write('\n');
        }
        return (rows.Count, names.ToArray());
    }

    internal static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny('\\', '\t', '\n') < 0 && !value.Contains('\r', StringComparison.Ordinal)) return value;
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\r", StringComparison.Ordinal);
    }
}
