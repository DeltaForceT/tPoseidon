using System.Runtime.CompilerServices;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public readonly record struct LibConfigTableSpan(string Name, int Start, int Length);

public sealed class LibConfigTableIndex
{
    private static readonly ConditionalWeakTable<byte[], LibConfigTableIndex> Built = new();
    private static readonly byte[] TableOpen = "<TABLE"u8.ToArray();
    private static readonly byte[] TableClose = "</TABLE>"u8.ToArray();

    private readonly Dictionary<string, LibConfigTableSpan> _firstByName;

    private LibConfigTableIndex(IReadOnlyList<LibConfigTableSpan> tables)
    {
        Tables = tables;
        _firstByName = new Dictionary<string, LibConfigTableSpan>(StringComparer.Ordinal);
        foreach (var table in tables) _firstByName.TryAdd(table.Name, table);
    }

    public IReadOnlyList<LibConfigTableSpan> Tables { get; }

    public static LibConfigTableIndex Of(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return Built.GetValue(xml, Build);
    }

    public bool TryFind(string name, out LibConfigTableSpan table) => _firstByName.TryGetValue(name, out table);

    private static LibConfigTableIndex Build(byte[] xml)
    {
        var tables = new List<LibConfigTableSpan>();
        var span = xml.AsSpan();
        var position = 0;
        while (position < span.Length)
        {
            var found = span[position..].IndexOf(TableOpen);
            if (found < 0) break;
            var start = position + found;
            var afterName = start + TableOpen.Length;
            if (afterName >= span.Length || !IsTagBreak(span[afterName]))
            {
                position = afterName;
                continue;
            }

            var tagEnd = span[afterName..].IndexOf((byte)'>');
            if (tagEnd < 0) break;
            var tag = Encoding.UTF8.GetString(span.Slice(start, afterName - start + tagEnd + 1));
            var closeAt = span[afterName..].IndexOf(TableClose);
            var end = closeAt < 0 ? span.Length : afterName + closeAt + TableClose.Length;
            if (LibConfigXmlStyle.Attribute(tag, "name") is { } name)
            {
                tables.Add(new LibConfigTableSpan(name, start, end - start));
            }
            position = end;
        }
        return new LibConfigTableIndex(tables);
    }

    private static bool IsTagBreak(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>';
}
