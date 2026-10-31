using System.Globalization;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigRowEdit(byte[] Xml, int Added, int Present);

public static class LibConfigXmlRowEditor
{
    private const string CdataOpen = "<![CDATA[";
    private const string CdataClose = "]]>";
    private const string StringType = "String";
    private static ReadOnlySpan<byte> RowClose => "\t\t</ROW>\n"u8;

    public static LibConfigRowEdit AddMissingRows(byte[] xml, LibConfigRowPatch patch)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(patch);
        var span = xml.AsSpan();
        var tableStart = LibConfigXmlStyle.TableStart(span, patch.Table);
        if (tableStart < 0)
        {
            throw new InvalidDataException($"libconfig has no table {patch.Table}.");
        }

        var headerEnd = tableStart + span[tableStart..].IndexOf((byte)'\n') + 1;
        var tableEnd = tableStart + span[tableStart..].IndexOf("\t</TABLE>"u8);
        if (tableEnd < tableStart)
        {
            throw new InvalidDataException($"libconfig table {patch.Table} has no end tag.");
        }

        var infos = ReadFieldInfos(span[headerEnd..tableEnd]);
        if (!infos.Select(info => info.Name).SequenceEqual(patch.Fields, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"libconfig table {patch.Table} has columns [{string.Join(",", infos.Select(info => info.Name))}], not the [{string.Join(",", patch.Fields)}] the patch was written for.");
        }

        var keyIndex = patch.Fields.ToList().IndexOf(patch.KeyField);
        if (keyIndex < 0)
        {
            throw new ArgumentException($"{patch.KeyField} is not a column of {patch.Table}.", nameof(patch));
        }

        var existing = ReadKeys(span[headerEnd..tableEnd], patch.KeyField);
        var missing = patch.Rows.Where(row => !existing.Contains(row[keyIndex])).ToList();
        if (missing.Count == 0)
        {
            return new LibConfigRowEdit(xml, 0, patch.Rows.Count);
        }

        var header = UpdateRowCount(span[tableStart..headerEnd], missing.Count);
        var rows = BuildRows(patch, infos, missing);
        using var output = new MemoryStream(xml.Length + rows.Length + 16);
        output.Write(span[..tableStart]);
        output.Write(header);
        output.Write(span[headerEnd..tableEnd]);
        output.Write(rows);
        output.Write(span[tableEnd..]);
        return new LibConfigRowEdit(output.ToArray(), missing.Count, patch.Rows.Count - missing.Count);
    }

    public static IReadOnlyList<string> MissingKeys(ReadOnlySpan<byte> xml, LibConfigRowPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var tableStart = LibConfigXmlStyle.TableStart(xml, patch.Table);
        if (tableStart < 0)
        {
            return patch.Rows.Select(row => row[patch.Fields.ToList().IndexOf(patch.KeyField)]).ToList();
        }

        var tableEnd = tableStart + xml[tableStart..].IndexOf("\t</TABLE>"u8);
        var keyIndex = patch.Fields.ToList().IndexOf(patch.KeyField);
        var existing = ReadKeys(xml[tableStart..tableEnd], patch.KeyField);
        return patch.Rows.Select(row => row[keyIndex]).Where(key => !existing.Contains(key)).ToList();
    }

    public static bool FieldsPending(ReadOnlySpan<byte> xml, LibConfigFieldPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        var table = FindTable(xml, patch.Table);
        if (table is not { } span)
        {
            return true;
        }

        var body = xml[span.Start..span.End];
        var rowStart = 0;
        while (NextRow(body, ref rowStart, out var row))
        {
            if (RowValue(row, patch.KeyField) != patch.Key)
            {
                continue;
            }

            foreach (var (field, value) in patch.Values)
            {
                if (RowValue(row, field) != value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static byte[] SetFields(byte[] xml, LibConfigFieldPatch patch)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(patch);
        var table = FindTable(xml, patch.Table) ?? throw new InvalidDataException($"libconfig has no table {patch.Table}.");
        var body = xml.AsSpan(table.Start, table.End - table.Start);
        using var output = new MemoryStream(xml.Length + 64);
        output.Write(xml, 0, table.Start);
        var position = 0;
        var matched = 0;
        var cursor = 0;
        while (NextRow(body, ref cursor, out var row))
        {
            var rowOffset = cursor - row.Length;
            if (RowValue(row, patch.KeyField) != patch.Key)
            {
                continue;
            }

            matched++;
            output.Write(body[position..rowOffset]);
            output.Write(RewriteRow(row, patch));
            position = cursor;
        }

        if (matched == 0)
        {
            throw new InvalidDataException($"libconfig table {patch.Table} has no row with {patch.KeyField} {patch.Key}.");
        }

        output.Write(body[position..]);
        output.Write(xml, table.End, xml.Length - table.End);
        return output.ToArray();
    }

    private readonly record struct TableSpan(int Start, int End);

    private static TableSpan? FindTable(ReadOnlySpan<byte> xml, string table)
    {
        var tableStart = LibConfigXmlStyle.TableStart(xml, table);
        if (tableStart < 0)
        {
            return null;
        }

        var tableEnd = tableStart + xml[tableStart..].IndexOf("	</TABLE>"u8);
        return new TableSpan(tableStart, tableEnd);
    }

    private static bool NextRow(ReadOnlySpan<byte> body, ref int cursor, out ReadOnlySpan<byte> row)
    {
        var open = body[cursor..].IndexOf("		<ROW>"u8);
        if (open < 0)
        {
            row = default;
            return false;
        }

        var start = cursor + open;
        var close = body[start..].IndexOf(RowClose);
        var end = start + close + RowClose.Length;
        row = body[start..end];
        cursor = end;
        return true;
    }

    private static string? RowValue(ReadOnlySpan<byte> row, string field)
    {
        var open = Encoding.ASCII.GetBytes($"			<{field}>");
        var found = row.IndexOf(open);
        if (found < 0)
        {
            return null;
        }

        var valueStart = found + open.Length;
        var length = row[valueStart..].IndexOf(Encoding.ASCII.GetBytes($"</{field}>"));
        var value = Encoding.UTF8.GetString(row.Slice(valueStart, length));
        return value.StartsWith(CdataOpen, StringComparison.Ordinal) && value.EndsWith(CdataClose, StringComparison.Ordinal)
            ? value[CdataOpen.Length..^CdataClose.Length]
            : value;
    }

    private static byte[] RewriteRow(ReadOnlySpan<byte> row, LibConfigFieldPatch patch)
    {
        var current = row.ToArray();
        foreach (var (field, value) in patch.Values)
        {
            var open = Encoding.ASCII.GetBytes($"			<{field}>");
            var close = Encoding.ASCII.GetBytes($"</{field}>");
            var found = current.AsSpan().IndexOf(open);
            if (found < 0)
            {
                throw new InvalidDataException($"libconfig table {patch.Table} has no column {field}.");
            }

            var valueStart = found + open.Length;
            var valueEnd = valueStart + current.AsSpan(valueStart).IndexOf(close);
            var isText = current.AsSpan(valueStart, valueEnd - valueStart).StartsWith(Encoding.ASCII.GetBytes(CdataOpen));
            var replacement = Encoding.UTF8.GetBytes(isText ? CdataOpen + value + CdataClose : value);
            current = [.. current[..valueStart], .. replacement, .. current[valueEnd..]];
        }

        return current;
    }

    private sealed record FieldInfo(string Name, string DataType);

    private static List<FieldInfo> ReadFieldInfos(ReadOnlySpan<byte> body)
    {
        var infos = new List<FieldInfo>();
        var text = Encoding.ASCII.GetString(body[..Math.Min(body.Length, FirstRowOffset(body))]);
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Attribute(line, "Name");
            var type = Attribute(line, "DataType");
            if (name is not null && type is not null)
            {
                infos.Add(new FieldInfo(name, type));
            }
        }

        return infos;
    }

    private static int FirstRowOffset(ReadOnlySpan<byte> body)
    {
        var row = body.IndexOf("\t\t<ROW>"u8);
        return row < 0 ? body.Length : row;
    }

    private static string? Attribute(string line, string name) => LibConfigXmlStyle.Attribute(line, name);

    private static HashSet<string> ReadKeys(ReadOnlySpan<byte> body, string keyField)
    {
        var open = Encoding.ASCII.GetBytes($"\t\t\t<{keyField}>");
        var close = Encoding.ASCII.GetBytes($"</{keyField}>");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        while (position < body.Length)
        {
            var found = body[position..].IndexOf(open);
            if (found < 0)
            {
                break;
            }

            var valueStart = position + found + open.Length;
            var length = body[valueStart..].IndexOf(close);
            if (length < 0)
            {
                break;
            }

            keys.Add(Encoding.ASCII.GetString(body.Slice(valueStart, length)));
            position = valueStart + length;
        }

        return keys;
    }

    private static byte[] UpdateRowCount(ReadOnlySpan<byte> header, int added)
    {
        var text = Encoding.ASCII.GetString(header);
        var value = LibConfigXmlStyle.AttributeValue(text, "RowCount")
            ?? throw new InvalidDataException("libconfig table header has no RowCount.");
        var count = int.Parse(value.Value, NumberStyles.None, CultureInfo.InvariantCulture) + added;
        return Encoding.ASCII.GetBytes(text[..value.Index] + count.ToString(CultureInfo.InvariantCulture) + text[(value.Index + value.Length)..]);
    }

    private static byte[] BuildRows(LibConfigRowPatch patch, List<FieldInfo> infos, List<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            if (row.Count != infos.Count)
            {
                throw new ArgumentException($"A row of {patch.Table} has {row.Count} values for {infos.Count} columns.", nameof(patch));
            }

            builder.Append("\t\t<ROW>\n");
            for (var i = 0; i < infos.Count; i++)
            {
                builder.Append("\t\t\t<").Append(infos[i].Name).Append('>');
                if (infos[i].DataType == StringType)
                {
                    if (row[i].Contains(CdataClose, StringComparison.Ordinal))
                    {
                        throw new ArgumentException($"{patch.Table}.{infos[i].Name} holds {CdataClose}.", nameof(patch));
                    }

                    builder.Append(CdataOpen).Append(row[i]).Append(CdataClose);
                }
                else
                {
                    builder.Append(row[i]);
                }

                builder.Append("</").Append(infos[i].Name).Append(">\n");
            }

            builder.Append("\t\t</ROW>\n");
        }

        return Encoding.ASCII.GetBytes(builder.ToString());
    }
}
