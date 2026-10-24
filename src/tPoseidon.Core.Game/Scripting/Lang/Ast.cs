namespace tPoseidon.Core.Game.Scripting.Lang;

public enum VariableScope : byte
{
    Character,
    Account,
    Server,
}

public enum EventKind : byte
{
    Login,
    LevelUp,
    MapEnter,
    MonsterKill,
    Named,
}

public enum TimerKind : byte
{
    Interval,
    DailyAt,
    Cron,
}

public abstract record Node(int Line, int Column);

public abstract record Declaration(int Line, int Column) : Node(Line, Column);

public sealed record NpcDeclaration(string Name, int Sprite, int Map, int X, int Y, int Facing, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public sealed record OverrideDeclaration(int Sprite, int? Map, int? X, int? Y, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public sealed record EventDeclaration(EventKind Kind, long? Number, string? Name, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public sealed record TimerDeclaration(TimerKind Kind, TimeSpan Interval, string Spec, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public sealed record CommandDeclaration(string Name, bool GameMasterOnly, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public sealed record FunctionDeclaration(string Name, IReadOnlyList<string> Parameters, IReadOnlyList<Statement> Body, int Line, int Column)
    : Declaration(Line, Column);

public abstract record Statement(int Line, int Column) : Node(Line, Column);

public sealed record LetStatement(string Name, Expression Value, int Line, int Column) : Statement(Line, Column);

public sealed record AssignStatement(Expression Target, string Operator, Expression Value, int Line, int Column) : Statement(Line, Column);

public sealed record ExpressionStatement(Expression Value, int Line, int Column) : Statement(Line, Column);

public sealed record IfStatement(Expression Condition, IReadOnlyList<Statement> Then, IReadOnlyList<Statement>? Else, int Line, int Column) : Statement(Line, Column);

public sealed record WhileStatement(Expression Condition, IReadOnlyList<Statement> Body, int Line, int Column) : Statement(Line, Column);

public sealed record BreakStatement(int Line, int Column) : Statement(Line, Column);

public sealed record ContinueStatement(int Line, int Column) : Statement(Line, Column);

public sealed record ReturnStatement(Expression? Value, int Line, int Column) : Statement(Line, Column);

public sealed record MesStatement(Expression Text, int Line, int Column) : Statement(Line, Column);

public sealed record NextStatement(int Line, int Column) : Statement(Line, Column);

public sealed record CloseStatement(int Line, int Column) : Statement(Line, Column);

public sealed record EndStatement(int Line, int Column) : Statement(Line, Column);

public sealed record FallbackStatement(int Line, int Column) : Statement(Line, Column);

public sealed record MenuOption(Expression Label, Expression? Condition, IReadOnlyList<Statement> Body, int Line, int Column) : Node(Line, Column);

public sealed record MenuStatement(IReadOnlyList<MenuOption> Options, int Line, int Column) : Statement(Line, Column);

public abstract record Expression(int Line, int Column) : Node(Line, Column);

public sealed record NumberLiteral(long Value, int Line, int Column) : Expression(Line, Column);

public sealed record TextLiteral(string Value, int Line, int Column) : Expression(Line, Column);

public sealed record LocalReference(string Name, int Line, int Column) : Expression(Line, Column);

public sealed record ScopedReference(VariableScope Scope, string Name, int Line, int Column) : Expression(Line, Column);

public sealed record UnaryExpression(string Operator, Expression Operand, int Line, int Column) : Expression(Line, Column);

public sealed record BinaryExpression(string Operator, Expression Left, Expression Right, int Line, int Column) : Expression(Line, Column);

public sealed record CallExpression(string Name, IReadOnlyList<Expression> Arguments, int Line, int Column) : Expression(Line, Column);

public sealed record MenuExpression(IReadOnlyList<Expression> Labels, int Line, int Column) : Expression(Line, Column);

public sealed record ScriptFileAst(IReadOnlyList<Declaration> Declarations);
