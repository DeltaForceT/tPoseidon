namespace tPoseidon.Core.Game.Scripting.Lang;

public enum TokenKind
{
    End,
    Identifier,
    Number,
    Text,
    Symbol,
}

public readonly record struct Token(TokenKind Kind, string Text, long Number, int Line, int Column)
{
    public bool IsSymbol(string symbol) => Kind == TokenKind.Symbol && Text == symbol;

    public bool IsWord(string word) => Kind == TokenKind.Identifier && Text == word;
}

public sealed class ScriptSyntaxException : Exception
{
    public ScriptSyntaxException(string message, int line, int column)
        : base(message)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }

    public int Column { get; }
}
