using System.Globalization;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigTableEdit(byte[] Xml, int Added, int Present);

public static class LibConfigXmlTableAdder
{
    private const string StringType = "String";
    private const string CdataOpen = "<![CDATA[";
    private const string CdataClose = "]]>";
    private const string TableCountAttribute = "TableCount";
    private static ReadOnlySpan<byte> IniClose => "</INI>"u8;
    private static ReadOnlySpan<byte> IniOpen => "<INI "u8;

    public static IReadOnlyList<string> MissingTables(ReadOnlySpan<byte> xml, IReadOnlyList<LibConfigTableDefinition> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        var missing = new List<string>();
        foreach (var table in tables)
        {
            if (LibConfigXmlStyle.TableStart(xml, table.Name) < 0)
            {
                missing.Add(table.Name);
            }
        }

        return missing;
    }

    public static LibConfigTableEdit AddMissingTables(byte[] xml, IReadOnlyList<LibConfigTableDefinition> tables)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(tables);
        var missing = MissingTables(xml, tables).ToHashSet(StringComparer.Ordinal);
        if (missing.Count == 0)
        {
            return new LibConfigTableEdit(xml, 0, tables.Count);
        }

        var span = xml.AsSpan();
        var close = span.LastIndexOf(IniClose);
        var open = span.IndexOf(IniOpen);
        if (close < 0 || open < 0)
        {
            throw new InvalidDataException("libconfig has no INI element.");
        }

        var headerEnd = open + span[open..].IndexOf((byte)'>');
        var header = Encoding.ASCII.GetString(span[open..headerEnd]);
        var count = LibConfigXmlStyle.AttributeValue(header, TableCountAttribute)
            ?? throw new InvalidDataException("libconfig INI element has no TableCount.");
        var spaced = header.Contains($"{TableCountAttribute} =", StringComparison.Ordinal);
        var quote = header[count.Index - 1];
        var newHeader = header[..count.Index]
            + (int.Parse(count.Value, NumberStyles.None, CultureInfo.InvariantCulture) + missing.Count).ToString(CultureInfo.InvariantCulture)
            + header[(count.Index + count.Length)..];

        var builder = new StringBuilder();
        foreach (var table in tables.Where(table => missing.Contains(table.Name)))
        {
            Append(builder, table, quote, spaced);
        }

        using var output = new MemoryStream(xml.Length + builder.Length * 2);
        output.Write(span[..open]);
        output.Write(Encoding.ASCII.GetBytes(newHeader));
        output.Write(span[headerEnd..close]);
        output.Write(Encoding.UTF8.GetBytes(builder.ToString()));
        output.Write(span[close..]);
        return new LibConfigTableEdit(output.ToArray(), missing.Count, tables.Count - missing.Count);
    }

    private static void Append(StringBuilder builder, LibConfigTableDefinition table, char quote, bool spaced)
    {
        var equals = spaced ? " = " : "=";
        builder.Append("\t<TABLE name=").Append(quote).Append(table.Name).Append(quote)
            .Append(" RowCount=").Append(quote).Append(table.Rows.Count.ToString(CultureInfo.InvariantCulture)).Append(quote)
            .Append(" TableInfoID").Append(equals).Append(quote).Append(table.InfoId.ToString(CultureInfo.InvariantCulture)).Append(quote)
            .Append(" FieldCnt").Append(equals).Append(quote).Append(table.Fields.Count.ToString(CultureInfo.InvariantCulture)).Append(quote)
            .Append(">\n");
        foreach (var field in table.Fields)
        {
            builder.Append("\t\t<FIELDINFO Name=").Append(quote).Append(field.Name).Append(quote)
                .Append(" IsKey=").Append(quote).Append(field.IsKey ? '1' : '0').Append(quote)
                .Append(" DataType=").Append(quote).Append(field.DataType).Append(quote)
                .Append("></FIELDINFO>\n");
        }

        foreach (var row in table.Rows)
        {
            if (row.Count != table.Fields.Count)
            {
                throw new InvalidDataException($"A row of {table.Name} has {row.Count} values for {table.Fields.Count} columns.");
            }

            builder.Append("\t\t<ROW>\n");
            for (var i = 0; i < table.Fields.Count; i++)
            {
                var field = table.Fields[i];
                builder.Append("\t\t\t<").Append(field.Name).Append('>');
                if (field.DataType == StringType)
                {
                    if (row[i].Contains(CdataClose, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException($"{table.Name}.{field.Name} holds {CdataClose}.");
                    }

                    builder.Append(CdataOpen).Append(row[i]).Append(CdataClose);
                }
                else
                {
                    builder.Append(row[i]);
                }

                builder.Append("</").Append(field.Name).Append(">\n");
            }

            builder.Append("\t\t</ROW>\n");
        }

        builder.Append("\t</TABLE>\n");
    }
}
