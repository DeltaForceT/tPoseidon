using System.Globalization;
using System.Text;

namespace tPoseidon.Core.DataCatalog.FastTable;

internal static class XmlText
{
    private const string CdataOpen = "<![CDATA[";
    private const string CdataClose = "]]>";

    public static string Unescape(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '\r')
            {
                builder.Append('\n');
                if (index + 1 < text.Length && text[index + 1] == '\n') index++;
                continue;
            }
            if (character == '<' && string.CompareOrdinal(text, index, CdataOpen, 0, CdataOpen.Length) == 0)
            {
                var close = text.IndexOf(CdataClose, index + CdataOpen.Length, StringComparison.Ordinal);
                if (close >= 0)
                {
                    builder.Append(text, index + CdataOpen.Length, close - index - CdataOpen.Length);
                    index = close + CdataClose.Length - 1;
                    continue;
                }
            }
            if (character != '&')
            {
                builder.Append(character);
                continue;
            }
            var end = text.IndexOf(';', index + 1);
            if (end < 0)
            {
                builder.Append(character);
                continue;
            }
            var entity = text.AsSpan(index + 1, end - index - 1);
            if (TryResolve(entity, out var resolved))
            {
                builder.Append(resolved);
                index = end;
            }
            else
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    private static bool TryResolve(ReadOnlySpan<char> entity, out string resolved)
    {
        resolved = string.Empty;
        switch (entity)
        {
            case "amp":
                resolved = "&";
                return true;
            case "lt":
                resolved = "<";
                return true;
            case "gt":
                resolved = ">";
                return true;
            case "quot":
                resolved = "\"";
                return true;
            case "apos":
                resolved = "'";
                return true;
        }
        if (entity.Length < 2 || entity[0] != '#') return false;
        var hex = entity[1] is 'x' or 'X';
        var digits = hex ? entity[2..] : entity[1..];
        if (!int.TryParse(digits, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None, CultureInfo.InvariantCulture, out var code)) return false;
        if (code is < 0 or > 0x10FFFF) return false;
        resolved = char.ConvertFromUtf32(code);
        return true;
    }
}
