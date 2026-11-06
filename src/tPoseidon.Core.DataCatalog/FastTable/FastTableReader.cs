using System.Text;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.DataCatalog.FastTable;

public static class FastTableReader
{
    private static readonly byte[] RowOpen = "<ROW"u8.ToArray();
    private static readonly byte[] RowClose = "</ROW>"u8.ToArray();

    public static void Read(byte[] xml, LibConfigTableSpan table, Action<RowView> handler, Action<RowView>? prepare = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(handler);
        var view = new RowView(xml);
        prepare?.Invoke(view);
        var positionNames = new List<byte[]>();
        var positionSlots = new List<int>();
        var end = table.Start + table.Length;
        var cursor = table.Start;
        while (cursor < end)
        {
            var open = xml.AsSpan(cursor, end - cursor).IndexOf(RowOpen);
            if (open < 0) break;
            var tagStart = cursor + open;
            var afterName = tagStart + RowOpen.Length;
            if (afterName >= end || !IsTagBreak(xml[afterName]))
            {
                cursor = afterName;
                continue;
            }
            var tagEnd = xml.AsSpan(afterName, end - afterName).IndexOf((byte)'>');
            if (tagEnd < 0) throw Malformed(table, "a row tag is not closed");
            var bodyStart = afterName + tagEnd + 1;
            if (xml[bodyStart - 2] == (byte)'/')
            {
                view.Begin();
                handler(view);
                cursor = bodyStart;
                continue;
            }
            var close = xml.AsSpan(bodyStart, end - bodyStart).IndexOf(RowClose);
            if (close < 0) throw Malformed(table, "a row is not closed");
            var bodyEnd = bodyStart + close;
            view.Begin();
            ParseFields(xml, bodyStart, bodyEnd, view, positionNames, positionSlots, table);
            handler(view);
            cursor = bodyEnd + RowClose.Length;
        }
    }

    private static InvalidDataException Malformed(LibConfigTableSpan table, string reason) =>
        new($"Table {table.Name} is malformed: {reason}.");

    private static void ParseFields(byte[] xml, int start, int end, RowView view, List<byte[]> positionNames, List<int> positionSlots, LibConfigTableSpan table)
    {
        var position = 0;
        var cursor = start;
        while (cursor < end)
        {
            var open = xml.AsSpan(cursor, end - cursor).IndexOf((byte)'<');
            if (open < 0) break;
            var nameStart = cursor + open + 1;
            if (nameStart >= end) break;
            if (xml[nameStart] == (byte)'!' || xml[nameStart] == (byte)'?')
            {
                var skipEnd = xml.AsSpan(nameStart, end - nameStart).IndexOf((byte)'>');
                if (skipEnd < 0) throw Malformed(table, "a comment or declaration is not closed");
                cursor = nameStart + skipEnd + 1;
                continue;
            }
            var nameEnd = nameStart;
            while (nameEnd < end && !IsTagBreak(xml[nameEnd]) && xml[nameEnd] != (byte)'/') nameEnd++;
            var name = xml.AsSpan(nameStart, nameEnd - nameStart);
            var tagClose = xml.AsSpan(nameEnd, end - nameEnd).IndexOf((byte)'>');
            if (tagClose < 0) throw Malformed(table, "a field tag is not closed");
            var tagCloseAt = nameEnd + tagClose;
            var slot = SlotAt(view, positionNames, positionSlots, position, name);
            position++;
            if (xml[tagCloseAt - 1] == (byte)'/')
            {
                view.Set(slot, tagCloseAt + 1, 0);
                cursor = tagCloseAt + 1;
                continue;
            }
            var valueStart = tagCloseAt + 1;
            var closeTag = FindClose(xml, valueStart, end, name);
            if (closeTag < 0) throw Malformed(table, "a field is not closed");
            view.Set(slot, valueStart, closeTag - valueStart);
            cursor = closeTag + name.Length + 3;
        }
    }

    private static int FindClose(byte[] xml, int from, int end, ReadOnlySpan<byte> name)
    {
        var cursor = from;
        while (cursor < end)
        {
            var open = xml.AsSpan(cursor, end - cursor).IndexOf((byte)'<');
            if (open < 0) return -1;
            var at = cursor + open;
            if (at + 1 < end && xml[at + 1] == (byte)'/' && xml.AsSpan(at + 2).StartsWith(name) && at + 2 + name.Length < end && xml[at + 2 + name.Length] == (byte)'>') return at;
            cursor = at + 1;
        }
        return -1;
    }

    private static int SlotAt(RowView view, List<byte[]> positionNames, List<int> positionSlots, int position, ReadOnlySpan<byte> name)
    {
        if (position < positionNames.Count && name.SequenceEqual(positionNames[position])) return positionSlots[position];
        var slot = view.SlotFor(Encoding.UTF8.GetString(name));
        if (position < positionNames.Count)
        {
            positionNames[position] = name.ToArray();
            positionSlots[position] = slot;
        }
        else
        {
            positionNames.Add(name.ToArray());
            positionSlots.Add(slot);
        }
        return slot;
    }

    private static bool IsTagBreak(byte value) => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'>';
}
