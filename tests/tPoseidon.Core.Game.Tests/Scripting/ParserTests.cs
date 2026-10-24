using tPoseidon.Core.Game.Scripting.Lang;

namespace tPoseidon.Core.GameTests.Scripting;

public class ParserTests
{
    private static void Lines(IEnumerable<string> actual, params string[] expected) => Assert.Equal(expected, actual);

    [Fact]
    public void Lexer_ReadsNumbersStringsAndSymbols()
    {
        var tokens = Lexer.Tokenize("let x = 0x10 + 7; // trailing\nmes \"a\\nb\";");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal(16, tokens[3].Number);
        Assert.Equal("+", tokens[4].Text);
        Assert.Equal("a\nb", tokens.Single(t => t.Kind == TokenKind.Text).Text);
        Assert.Equal(2, tokens.First(t => t.IsWord("mes")).Line);
    }

    [Fact]
    public void Lexer_RejectsAnUnterminatedString()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => Lexer.Tokenize("mes \"open;"));
        Assert.Equal(1, error.Line);
    }

    [Fact]
    public void Lexer_RejectsAnUnterminatedBlockComment()
    {
        Assert.Throws<ScriptSyntaxException>(() => Lexer.Tokenize("/* never closed"));
    }

    [Fact]
    public void Parser_ReadsAnNpcDeclaration()
    {
        var ast = Parser.Parse("npc \"Mira\" sprite 1301 map 1 at 120, 340 face east { mes \"Hi\"; close; }");
        var npc = Assert.IsType<NpcDeclaration>(Assert.Single(ast.Declarations));
        Assert.Equal("Mira", npc.Name);
        Assert.Equal(1301, npc.Sprite);
        Assert.Equal(1, npc.Map);
        Assert.Equal(120, npc.X);
        Assert.Equal(340, npc.Y);
        Assert.Equal(90, npc.Facing);
        Assert.Equal(2, npc.Body.Count);
    }

    [Fact]
    public void Parser_ReadsAnOverrideWithFilters()
    {
        var ast = Parser.Parse("override npc 77 map 5 at 10, 20 { end; }");
        var over = Assert.IsType<OverrideDeclaration>(Assert.Single(ast.Declarations));
        Assert.Equal(77, over.Sprite);
        Assert.Equal(5, over.Map);
        Assert.Equal(10, over.X);
        Assert.Equal(20, over.Y);
    }

    [Fact]
    public void Parser_ReadsEventsTimersCommandsAndFunctions()
    {
        var ast = Parser.Parse(
            "on login { end; } on mapenter 5 { end; } on kill 1001 { end; } on event \"boss\" { end; }\n"
            + "every 30 minutes { end; } at \"20:00\" { end; } cron \"*/5 * * * *\" { end; }\n"
            + "command \"reload\" gm { end; } function add(a, b) { return a + b; }");
        Assert.Equal(9, ast.Declarations.Count);
        var kill = Assert.IsType<EventDeclaration>(ast.Declarations[2]);
        Assert.Equal(EventKind.MonsterKill, kill.Kind);
        Assert.Equal(1001, kill.Number);
        var every = Assert.IsType<TimerDeclaration>(ast.Declarations[4]);
        Assert.Equal(TimeSpan.FromMinutes(30), every.Interval);
        var command = Assert.IsType<CommandDeclaration>(ast.Declarations[7]);
        Assert.True(command.GameMasterOnly);
        var function = Assert.IsType<FunctionDeclaration>(ast.Declarations[8]);
        Lines(function.Parameters, "a", "b");
    }

    [Fact]
    public void Parser_ReadsMenusWithConditionsAndScopedVariables()
    {
        var ast = Parser.Parse(
            "npc \"A\" sprite 1 map 1 at 1, 1 { menu { \"Yes\" { char.said = 1; } \"VIP\" if (account.vip == 1) { end; } } }");
        var npc = Assert.IsType<NpcDeclaration>(Assert.Single(ast.Declarations));
        var menu = Assert.IsType<MenuStatement>(Assert.Single(npc.Body));
        Assert.Equal(2, menu.Options.Count);
        Assert.NotNull(menu.Options[1].Condition);
        var assign = Assert.IsType<AssignStatement>(menu.Options[0].Body[0]);
        var target = Assert.IsType<ScopedReference>(assign.Target);
        Assert.Equal(VariableScope.Character, target.Scope);
        Assert.Equal("said", target.Name);
    }

    [Fact]
    public void Parser_ReportsLineAndColumnOfASyntaxError()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => Parser.Parse("npc \"A\" sprite 1 map 1 at 1, 1 {\n  mes \"x\"\n}"));
        Assert.Equal(3, error.Line);
        Assert.Contains("';'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_RequiresAPositionForAnNpc()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => Parser.Parse("npc \"A\" sprite 1 { end; }"));
        Assert.Contains("map", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parser_RejectsAssigningToACall()
    {
        Assert.Throws<ScriptSyntaxException>(() => Parser.Parse("function f() { level() = 3; }"));
    }

    [Fact]
    public void Parser_RejectsAnUnknownDeclaration()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => Parser.Parse("spawn 1;"));
        Assert.Equal(1, error.Line);
    }
}
