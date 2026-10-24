namespace tPoseidon.Core.Game.Scripting.Lang;

public sealed class Parser
{
    private static readonly Dictionary<string, int> Facings = new(StringComparer.Ordinal)
    {
        ["north"] = 0,
        ["northeast"] = 45,
        ["east"] = 90,
        ["southeast"] = 135,
        ["south"] = 180,
        ["southwest"] = 225,
        ["west"] = 270,
        ["northwest"] = 315,
    };

    private static readonly Dictionary<string, VariableScope> Scopes = new(StringComparer.Ordinal)
    {
        ["char"] = VariableScope.Character,
        ["account"] = VariableScope.Account,
        ["server"] = VariableScope.Server,
    };

    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "let", "if", "else", "while", "break", "continue", "return", "mes", "next", "close", "end", "fallback",
        "menu", "npc", "override", "on", "every", "at", "cron", "command", "function", "char", "account", "server",
    };

    private readonly IReadOnlyList<Token> _tokens;
    private int _position;

    private Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ScriptFileAst Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var parser = new Parser(Lexer.Tokenize(source));
        var declarations = new List<Declaration>();
        while (parser.Current.Kind != TokenKind.End) declarations.Add(parser.ParseDeclaration());
        return new ScriptFileAst(declarations);
    }

    private Token Current => _tokens[_position];

    private Token Peek(int offset) => _tokens[Math.Min(_position + offset, _tokens.Count - 1)];

    private Token Advance() => _tokens[_position++];

    private static ScriptSyntaxException Error(string message, Token at) => new(message, at.Line, at.Column);

    private Token ExpectSymbol(string symbol)
    {
        if (!Current.IsSymbol(symbol)) throw Error($"Expected '{symbol}' but found {Describe(Current)}", Current);
        return Advance();
    }

    private Token ExpectWord(string word)
    {
        if (!Current.IsWord(word)) throw Error($"Expected '{word}' but found {Describe(Current)}", Current);
        return Advance();
    }

    private string ExpectName(string what)
    {
        if (Current.Kind != TokenKind.Identifier || Reserved.Contains(Current.Text)) throw Error($"Expected {what} but found {Describe(Current)}", Current);
        return Advance().Text;
    }

    private long ExpectNumber(string what)
    {
        if (Current.Kind != TokenKind.Number) throw Error($"Expected {what} but found {Describe(Current)}", Current);
        return Advance().Number;
    }

    private string ExpectString(string what)
    {
        if (Current.Kind != TokenKind.Text) throw Error($"Expected {what} but found {Describe(Current)}", Current);
        return Advance().Text;
    }

    private static string Describe(Token token) => token.Kind switch
    {
        TokenKind.End => "the end of the file",
        TokenKind.Text => "a string",
        TokenKind.Number => $"the number {token.Text}",
        _ => $"'{token.Text}'",
    };

    private Declaration ParseDeclaration()
    {
        var start = Current;
        if (start.Kind != TokenKind.Identifier) throw Error($"Expected a declaration but found {Describe(start)}", start);
        switch (start.Text)
        {
            case "npc":
                return ParseNpc();
            case "override":
                return ParseOverride();
            case "on":
                return ParseEvent();
            case "every":
            case "at":
            case "cron":
                return ParseTimer();
            case "command":
                return ParseCommand();
            case "function":
                return ParseFunction();
            default:
                throw Error($"Expected npc, override, on, every, at, cron, command or function but found '{start.Text}'", start);
        }
    }

    private NpcDeclaration ParseNpc()
    {
        var start = ExpectWord("npc");
        var name = ExpectString("the NPC name in quotes");
        int? sprite = null;
        int? map = null;
        int? x = null;
        int? y = null;
        var facing = 180;
        while (!Current.IsSymbol("{"))
        {
            var key = Current;
            if (key.Kind != TokenKind.Identifier) throw Error($"Expected sprite, map, at, face or '{{' but found {Describe(key)}", key);
            Advance();
            switch (key.Text)
            {
                case "sprite":
                    sprite = checked((int)ExpectNumber("the sprite (NPC type) id"));
                    break;
                case "map":
                    map = checked((int)ExpectNumber("the map id"));
                    break;
                case "at":
                    x = checked((int)ExpectNumber("the x position"));
                    ExpectSymbol(",");
                    y = checked((int)ExpectNumber("the y position"));
                    break;
                case "face":
                    facing = ParseFacing();
                    break;
                default:
                    throw Error($"Unknown NPC property '{key.Text}'; use sprite, map, at or face", key);
            }
        }
        if (sprite is null) throw Error("An npc needs 'sprite <type id>'", start);
        if (map is null || x is null || y is null) throw Error("An npc needs 'map <id> at <x>, <y>'", start);
        return new NpcDeclaration(name, sprite.Value, map.Value, x.Value, y.Value, facing, ParseBlock(), start.Line, start.Column);
    }

    private int ParseFacing()
    {
        if (Current.Kind == TokenKind.Number) return (int)(Advance().Number % 360);
        var word = Current;
        if (word.Kind == TokenKind.Identifier && Facings.TryGetValue(word.Text, out var degrees))
        {
            Advance();
            return degrees;
        }
        throw Error("Expected a direction (north, northeast, east, southeast, south, southwest, west, northwest) or degrees", word);
    }

    private OverrideDeclaration ParseOverride()
    {
        var start = ExpectWord("override");
        ExpectWord("npc");
        var sprite = checked((int)ExpectNumber("the NPC type id"));
        int? map = null;
        int? x = null;
        int? y = null;
        while (!Current.IsSymbol("{"))
        {
            var key = Current;
            if (key.Kind != TokenKind.Identifier) throw Error($"Expected map, at or '{{' but found {Describe(key)}", key);
            Advance();
            switch (key.Text)
            {
                case "map":
                    map = checked((int)ExpectNumber("the map id"));
                    break;
                case "at":
                    x = checked((int)ExpectNumber("the x position"));
                    ExpectSymbol(",");
                    y = checked((int)ExpectNumber("the y position"));
                    break;
                default:
                    throw Error($"Unknown override filter '{key.Text}'; use map or at", key);
            }
        }
        return new OverrideDeclaration(sprite, map, x, y, ParseBlock(), start.Line, start.Column);
    }

    private EventDeclaration ParseEvent()
    {
        var start = ExpectWord("on");
        var kindToken = Current;
        if (kindToken.Kind != TokenKind.Identifier) throw Error($"Expected an event name after 'on' but found {Describe(kindToken)}", kindToken);
        Advance();
        long? number = null;
        string? name = null;
        EventKind kind;
        switch (kindToken.Text)
        {
            case "login":
                kind = EventKind.Login;
                break;
            case "levelup":
                kind = EventKind.LevelUp;
                break;
            case "mapenter":
                kind = EventKind.MapEnter;
                if (Current.Kind == TokenKind.Number) number = Advance().Number;
                break;
            case "kill":
                kind = EventKind.MonsterKill;
                if (Current.Kind == TokenKind.Number) number = Advance().Number;
                break;
            case "event":
                kind = EventKind.Named;
                name = ExpectString("the event name in quotes");
                break;
            default:
                throw Error($"Unknown event '{kindToken.Text}'; use login, levelup, mapenter, kill or event", kindToken);
        }
        return new EventDeclaration(kind, number, name, ParseBlock(), start.Line, start.Column);
    }

    private TimerDeclaration ParseTimer()
    {
        var start = Advance();
        TimerKind kind;
        var interval = TimeSpan.Zero;
        string spec;
        switch (start.Text)
        {
            case "every":
                kind = TimerKind.Interval;
                var amount = ExpectNumber("the interval length");
                var unit = Current;
                if (unit.Kind != TokenKind.Identifier) throw Error("Expected seconds, minutes or hours", unit);
                Advance();
                interval = unit.Text switch
                {
                    "second" or "seconds" => TimeSpan.FromSeconds(amount),
                    "minute" or "minutes" => TimeSpan.FromMinutes(amount),
                    "hour" or "hours" => TimeSpan.FromHours(amount),
                    _ => throw Error($"Unknown time unit '{unit.Text}'; use seconds, minutes or hours", unit),
                };
                spec = $"{amount} {unit.Text}";
                break;
            case "at":
                kind = TimerKind.DailyAt;
                spec = ExpectString("a time such as \"20:30\"");
                break;
            default:
                kind = TimerKind.Cron;
                spec = ExpectString("a five field cron expression in quotes");
                break;
        }
        return new TimerDeclaration(kind, interval, spec, ParseBlock(), start.Line, start.Column);
    }

    private CommandDeclaration ParseCommand()
    {
        var start = ExpectWord("command");
        var name = ExpectString("the command name in quotes");
        var gameMasterOnly = false;
        if (Current.IsWord("gm"))
        {
            Advance();
            gameMasterOnly = true;
        }
        return new CommandDeclaration(name, gameMasterOnly, ParseBlock(), start.Line, start.Column);
    }

    private FunctionDeclaration ParseFunction()
    {
        var start = ExpectWord("function");
        var name = ExpectName("a function name");
        ExpectSymbol("(");
        var parameters = new List<string>();
        if (!Current.IsSymbol(")"))
        {
            do
            {
                parameters.Add(ExpectName("a parameter name"));
            }
            while (TryConsume(","));
        }
        ExpectSymbol(")");
        return new FunctionDeclaration(name, parameters, ParseBlock(), start.Line, start.Column);
    }

    private bool TryConsume(string symbol)
    {
        if (!Current.IsSymbol(symbol)) return false;
        Advance();
        return true;
    }

    private List<Statement> ParseBlock()
    {
        ExpectSymbol("{");
        var statements = new List<Statement>();
        while (!Current.IsSymbol("}"))
        {
            if (Current.Kind == TokenKind.End) throw Error("A block is never closed; expected '}'", Current);
            statements.Add(ParseStatement());
        }
        Advance();
        return statements;
    }

    private Statement ParseStatement()
    {
        var token = Current;
        if (token.Kind == TokenKind.Identifier)
        {
            switch (token.Text)
            {
                case "let":
                    return ParseLet();
                case "if":
                    return ParseIf();
                case "while":
                    return ParseWhile();
                case "break":
                    Advance();
                    ExpectSymbol(";");
                    return new BreakStatement(token.Line, token.Column);
                case "continue":
                    Advance();
                    ExpectSymbol(";");
                    return new ContinueStatement(token.Line, token.Column);
                case "return":
                    Advance();
                    Expression? value = null;
                    if (!Current.IsSymbol(";")) value = ParseExpression();
                    ExpectSymbol(";");
                    return new ReturnStatement(value, token.Line, token.Column);
                case "mes":
                    Advance();
                    var text = ParseExpression();
                    ExpectSymbol(";");
                    return new MesStatement(text, token.Line, token.Column);
                case "next":
                    Advance();
                    ExpectSymbol(";");
                    return new NextStatement(token.Line, token.Column);
                case "close":
                    Advance();
                    ExpectSymbol(";");
                    return new CloseStatement(token.Line, token.Column);
                case "end":
                    Advance();
                    ExpectSymbol(";");
                    return new EndStatement(token.Line, token.Column);
                case "fallback":
                    Advance();
                    ExpectSymbol(";");
                    return new FallbackStatement(token.Line, token.Column);
                case "menu" when Peek(1).IsSymbol("{"):
                    return ParseMenu();
            }
        }
        var expression = ParseExpression();
        if (Current.Kind == TokenKind.Symbol && Current.Text is "=" or "+=" or "-=" or "*=" or "/=" or "%=")
        {
            var op = Advance();
            if (expression is not (LocalReference or ScopedReference)) throw Error("Only a variable can be assigned to", op);
            var value = ParseExpression();
            ExpectSymbol(";");
            return new AssignStatement(expression, op.Text, value, token.Line, token.Column);
        }
        ExpectSymbol(";");
        return new ExpressionStatement(expression, token.Line, token.Column);
    }

    private LetStatement ParseLet()
    {
        var start = ExpectWord("let");
        var name = ExpectName("a variable name");
        ExpectSymbol("=");
        var value = ParseExpression();
        ExpectSymbol(";");
        return new LetStatement(name, value, start.Line, start.Column);
    }

    private IfStatement ParseIf()
    {
        var start = ExpectWord("if");
        ExpectSymbol("(");
        var condition = ParseExpression();
        ExpectSymbol(")");
        var then = ParseBlock();
        List<Statement>? otherwise = null;
        if (Current.IsWord("else"))
        {
            Advance();
            otherwise = Current.IsWord("if") ? new List<Statement> { ParseIf() } : ParseBlock();
        }
        return new IfStatement(condition, then, otherwise, start.Line, start.Column);
    }

    private WhileStatement ParseWhile()
    {
        var start = ExpectWord("while");
        ExpectSymbol("(");
        var condition = ParseExpression();
        ExpectSymbol(")");
        return new WhileStatement(condition, ParseBlock(), start.Line, start.Column);
    }

    private MenuStatement ParseMenu()
    {
        var start = ExpectWord("menu");
        ExpectSymbol("{");
        var options = new List<MenuOption>();
        while (!Current.IsSymbol("}"))
        {
            if (Current.Kind == TokenKind.End) throw Error("A menu is never closed; expected '}'", Current);
            var label = ParseExpression();
            Expression? condition = null;
            if (Current.IsWord("if"))
            {
                Advance();
                ExpectSymbol("(");
                condition = ParseExpression();
                ExpectSymbol(")");
            }
            var body = ParseBlock();
            options.Add(new MenuOption(label, condition, body, label.Line, label.Column));
        }
        Advance();
        if (options.Count == 0) throw Error("A menu needs at least one option", start);
        return new MenuStatement(options, start.Line, start.Column);
    }

    private Expression ParseExpression() => ParseOr();

    private Expression ParseOr()
    {
        var left = ParseAnd();
        while (Current.IsSymbol("||"))
        {
            var op = Advance();
            left = new BinaryExpression("||", left, ParseAnd(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseAnd()
    {
        var left = ParseEquality();
        while (Current.IsSymbol("&&"))
        {
            var op = Advance();
            left = new BinaryExpression("&&", left, ParseEquality(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseEquality()
    {
        var left = ParseComparison();
        while (Current.IsSymbol("==") || Current.IsSymbol("!="))
        {
            var op = Advance();
            left = new BinaryExpression(op.Text, left, ParseComparison(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseComparison()
    {
        var left = ParseAdditive();
        while (Current.IsSymbol("<") || Current.IsSymbol("<=") || Current.IsSymbol(">") || Current.IsSymbol(">="))
        {
            var op = Advance();
            left = new BinaryExpression(op.Text, left, ParseAdditive(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.IsSymbol("+") || Current.IsSymbol("-"))
        {
            var op = Advance();
            left = new BinaryExpression(op.Text, left, ParseMultiplicative(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Current.IsSymbol("*") || Current.IsSymbol("/") || Current.IsSymbol("%"))
        {
            var op = Advance();
            left = new BinaryExpression(op.Text, left, ParseUnary(), op.Line, op.Column);
        }
        return left;
    }

    private Expression ParseUnary()
    {
        if (Current.IsSymbol("!") || Current.IsSymbol("-"))
        {
            var op = Advance();
            return new UnaryExpression(op.Text, ParseUnary(), op.Line, op.Column);
        }
        return ParsePrimary();
    }

    private Expression ParsePrimary()
    {
        var token = Current;
        switch (token.Kind)
        {
            case TokenKind.Number:
                Advance();
                return new NumberLiteral(token.Number, token.Line, token.Column);
            case TokenKind.Text:
                Advance();
                return new TextLiteral(token.Text, token.Line, token.Column);
            case TokenKind.Symbol when token.Text == "(":
                Advance();
                var inner = ParseExpression();
                ExpectSymbol(")");
                return inner;
            case TokenKind.Identifier:
                return ParseIdentifierExpression();
            default:
                throw Error($"Expected a value but found {Describe(token)}", token);
        }
    }

    private Expression ParseIdentifierExpression()
    {
        var token = Advance();
        if (Scopes.TryGetValue(token.Text, out var scope) && Current.IsSymbol("."))
        {
            Advance();
            var name = Current;
            if (name.Kind != TokenKind.Identifier) throw Error($"Expected a variable name after '{token.Text}.' but found {Describe(name)}", name);
            Advance();
            return new ScopedReference(scope, name.Text, token.Line, token.Column);
        }
        if (Current.IsSymbol("("))
        {
            Advance();
            var arguments = new List<Expression>();
            if (!Current.IsSymbol(")"))
            {
                do
                {
                    arguments.Add(ParseExpression());
                }
                while (TryConsume(","));
            }
            ExpectSymbol(")");
            if (token.Text == "menu")
            {
                if (arguments.Count == 0) throw Error("menu() needs at least one option", token);
                return new MenuExpression(arguments, token.Line, token.Column);
            }
            return new CallExpression(token.Text, arguments, token.Line, token.Column);
        }
        if (Reserved.Contains(token.Text)) throw Error($"'{token.Text}' is a keyword and cannot be used as a value", token);
        return new LocalReference(token.Text, token.Line, token.Column);
    }
}
