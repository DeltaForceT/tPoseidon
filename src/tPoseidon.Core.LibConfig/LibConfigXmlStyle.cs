using System.Text;
using System.Text.RegularExpressions;

namespace tPoseidon.Core.LibConfig;

public static class LibConfigXmlStyle
{
    private const byte SingleQuote = (byte)'\'';
    private const byte DoubleQuote = (byte)'"';
    private static readonly byte[] QuoteBytes = [SingleQuote, DoubleQuote];

    public static bool IsQuote(byte value) => value is SingleQuote or DoubleQuote;

    public static int TableStart(ReadOnlySpan<byte> xml, string table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var start = -1;
        foreach (var quote in QuoteBytes)
        {
            var prefix = Encoding.ASCII.GetBytes($"\t<TABLE name={(char)quote}{table}{(char)quote}");
            var position = 0;
            while (position < xml.Length)
            {
                var found = xml[position..].IndexOf(prefix);
                if (found < 0)
                {
                    break;
                }

                var at = position + found;
                var after = at + prefix.Length;
                if (after < xml.Length && xml[after] is (byte)' ' or (byte)'>')
                {
                    start = start < 0 ? at : Math.Min(start, at);
                    break;
                }

                position = after;
            }
        }

        return start;
    }

    public static string? Attribute(string line, string name) => AttributeValue(line, name)?.Value;

    public static Group? AttributeValue(string line, string name)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(name);
        var match = Regex.Match(line, $@"(?<![A-Za-z]){Regex.Escape(name)}\s*=\s*(['""])(.*?)\1", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[2] : null;
    }
}
