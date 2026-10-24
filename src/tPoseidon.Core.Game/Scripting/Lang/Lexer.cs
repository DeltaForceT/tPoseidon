using System.Globalization;
using System.Text;

namespace tPoseidon.Core.Game.Scripting.Lang;

public static class Lexer
{
    private static readonly string[] TwoCharacterSymbols =
    {
        "==", "!=", "<=", ">=", "&&", "||", "+=", "-=", "*=", "/=", "%=",
    };

    private const string SingleCharacterSymbols = "{}()[],;.+-*/%<>=!:";

    public static IReadOnlyList<Token> Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var tokens = new List<Token>();
        var index = 0;
        var line = 1;
        var lineStart = 0;
        while (index < source.Length)
        {
            var c = source[index];
            if (c == '\n')
            {
                line++;
                index++;
                lineStart = index;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                index++;
                continue;
            }
            var column = index - lineStart + 1;
            if ((c == '/' && index + 1 < source.Length && source[index + 1] == '/') || c == '#')
            {
                while (index < source.Length && source[index] != '\n') index++;
                continue;
            }
            if (c == '/' && index + 1 < source.Length && source[index + 1] == '*')
            {
                var startLine = line;
                index += 2;
                while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/'))
                {
                    if (source[index] == '\n')
                    {
                        line++;
                        lineStart = index + 1;
                    }
                    index++;
                }
                if (index + 1 >= source.Length) throw new ScriptSyntaxException("A block comment is never closed", startLine, column);
                index += 2;
                continue;
            }
            if (c == '"')
            {
                tokens.Add(ReadString(source, ref index, line, column));
                continue;
            }
            if (char.IsDigit(c))
            {
                tokens.Add(ReadNumber(source, ref index, line, column));
                continue;
            }
            if (char.IsLetter(c) || c == '_')
            {
                var start = index;
                while (index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_')) index++;
                tokens.Add(new Token(TokenKind.Identifier, source[start..index], 0, line, column));
                continue;
            }
            if (index + 1 < source.Length)
            {
                var pair = source.Substring(index, 2);
                if (Array.IndexOf(TwoCharacterSymbols, pair) >= 0)
                {
                    tokens.Add(new Token(TokenKind.Symbol, pair, 0, line, column));
                    index += 2;
                    continue;
                }
            }
            if (SingleCharacterSymbols.Contains(c, StringComparison.Ordinal))
            {
                tokens.Add(new Token(TokenKind.Symbol, c.ToString(), 0, line, column));
                index++;
                continue;
            }
            throw new ScriptSyntaxException($"Unexpected character '{c}'", line, column);
        }
        tokens.Add(new Token(TokenKind.End, string.Empty, 0, line, index - lineStart + 1));
        return tokens;
    }

    private static Token ReadNumber(string source, ref int index, int line, int column)
    {
        var start = index;
        if (source[index] == '0' && index + 1 < source.Length && (source[index + 1] == 'x' || source[index + 1] == 'X'))
        {
            index += 2;
            while (index < source.Length && Uri.IsHexDigit(source[index])) index++;
            if (index - start == 2 || !long.TryParse(source.AsSpan(start + 2, index - start - 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex))
            {
                throw new ScriptSyntaxException("A hexadecimal number needs digits that fit 64 bits", line, column);
            }
            return new Token(TokenKind.Number, source[start..index], hex, line, column);
        }
        while (index < source.Length && char.IsDigit(source[index])) index++;
        if (!long.TryParse(source.AsSpan(start, index - start), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new ScriptSyntaxException("The number does not fit 64 bits", line, column);
        }
        return new Token(TokenKind.Number, source[start..index], value, line, column);
    }

    private static Token ReadString(string source, ref int index, int line, int column)
    {
        var text = new StringBuilder();
        index++;
        while (true)
        {
            if (index >= source.Length || source[index] == '\n') throw new ScriptSyntaxException("A string is not closed on its line", line, column);
            var c = source[index++];
            if (c == '"') break;
            if (c != '\\')
            {
                text.Append(c);
                continue;
            }
            if (index >= source.Length) throw new ScriptSyntaxException("A string is not closed on its line", line, column);
            var escaped = source[index++];
            switch (escaped)
            {
                case 'n':
                    text.Append('\n');
                    break;
                case 't':
                    text.Append('\t');
                    break;
                case '"':
                    text.Append('"');
                    break;
                case '\\':
                    text.Append('\\');
                    break;
                default:
                    throw new ScriptSyntaxException($"Unknown escape '\\{escaped}' in a string", line, column);
            }
        }
        return new Token(TokenKind.Text, text.ToString(), 0, line, column);
    }
}
