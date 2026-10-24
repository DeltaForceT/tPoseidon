using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Vm;

namespace tPoseidon.Core.GameTests.Scripting;

public class MachineTests
{
    private static (ScriptMachine Machine, FakeScriptHost Host) Machine(string body, string functions = "", int budget = ScriptMachine.DefaultInstructionBudget)
    {
        var file = ScriptCompiler.Compile("test.tscript", functions + " npc \"T\" sprite 1 map 1 at 1, 1 { " + body + " }");
        var library = file.Functions.ToDictionary(function => function.Name, StringComparer.Ordinal);
        var machine = new ScriptMachine(file.Npcs[0].Talk, name => library.GetValueOrDefault(name), budget);
        return (machine, new FakeScriptHost());
    }

    private static void Lines(IEnumerable<string> actual, params string[] expected) => Assert.Equal(expected, actual);

    [Fact]
    public void Mes_AndClose_ProducesAClosingPage()
    {
        var (machine, host) = Machine("mes \"Hello\"; mes \"World\"; close;");
        var result = machine.Start(host);
        Assert.Equal(RunState.Closed, result.State);
        Lines(result.Page!.Lines, "Hello", "World");
        Assert.Equal(PageKind.Close, result.Page.Kind);
        Assert.True(machine.IsFinished);
    }

    [Fact]
    public void Next_SuspendsAndContinues()
    {
        var (machine, host) = Machine("mes \"One\"; next; mes \"Two\"; close;");
        var first = machine.Start(host);
        Assert.Equal(RunState.AwaitingNext, first.State);
        Lines(first.Page!.Lines, "One");
        var second = machine.Continue(host);
        Assert.Equal(RunState.Closed, second.State);
        Lines(second.Page!.Lines, "Two");
    }

    [Fact]
    public void End_WithoutText_FinishesWithoutAPage()
    {
        var (machine, host) = Machine("end;");
        var result = machine.Start(host);
        Assert.Equal(RunState.Ended, result.State);
        Assert.Null(result.Page);
    }

    [Fact]
    public void Menu_RunsTheChosenBranch()
    {
        var (machine, host) = Machine("mes \"Pick\"; menu { \"A\" { mes \"chose A\"; close; } \"B\" { mes \"chose B\"; close; } }");
        var menu = machine.Start(host);
        Assert.Equal(RunState.AwaitingMenu, menu.State);
        Lines(menu.Page!.Options, "A", "B");
        var result = machine.Choose(host, 2);
        Lines(result.Page!.Lines, "chose B");
    }

    [Fact]
    public void Menu_HidesOptionsWhoseConditionIsFalse()
    {
        var (machine, host) = Machine("menu { \"A\" { mes \"a\"; close; } \"B\" if (level() > 100) { mes \"b\"; close; } \"C\" { mes \"c\"; close; } }");
        var menu = machine.Start(host);
        Lines(menu.Page!.Options, "A", "C");
        var result = machine.Choose(host, 2);
        Lines(result.Page!.Lines, "c");
    }

    [Fact]
    public void MenuExpression_ReturnsTheChoice()
    {
        var (machine, host) = Machine("let pick = menu(\"X\", \"Y\", \"Z\"); mes \"picked \" + pick; close;");
        machine.Start(host);
        var result = machine.Choose(host, 3);
        Lines(result.Page!.Lines, "picked 3");
    }

    [Fact]
    public void Choose_RejectsAnOutOfRangeChoice()
    {
        var (machine, host) = Machine("menu { \"A\" { end; } }");
        machine.Start(host);
        Assert.Throws<ScriptRuntimeException>(() => machine.Choose(host, 2));
    }

    [Fact]
    public void Arithmetic_Comparison_AndTextConcatenation()
    {
        var (machine, host) = Machine("let a = 2 + 3 * 4; let b = (2 + 3) * 4; mes a + \",\" + b + \",\" + (10 % 4) + \",\" + (7 / 2) + \",\" + (a > b) + \",\" + (\"x\" == \"x\"); close;");
        Assert.Equal("14,20,2,3,0,1", machine.Start(host).Page!.Lines[0]);
    }

    [Fact]
    public void WhileLoop_WithBreakAndContinue()
    {
        var (machine, host) = Machine(
            "let i = 0; let sum = 0; while (i < 10) { i += 1; if (i == 3) { continue; } if (i == 6) { break; } sum += i; } mes sum; close;");
        Assert.Equal("12", machine.Start(host).Page!.Lines[0]);
    }

    [Fact]
    public void IfElseChains()
    {
        var (machine, host) = Machine("let n = 5; if (n < 3) { mes \"low\"; } else if (n < 6) { mes \"mid\"; } else { mes \"high\"; } close;");
        Assert.Equal("mid", machine.Start(host).Page!.Lines[0]);
    }

    [Fact]
    public void ShortCircuit_DoesNotEvaluateTheRightSide()
    {
        var (machine, host) = Machine("if (0 && 1 / 0) { mes \"no\"; } if (1 || 1 / 0) { mes \"yes\"; } close;");
        Lines(machine.Start(host).Page!.Lines, "yes");
    }

    [Fact]
    public void Functions_TakeParametersAndReturnValues()
    {
        var (machine, host) = Machine(
            "mes square(6) + fact(5); close;",
            "function square(n) { return n * n; } function fact(n) { if (n <= 1) { return 1; } return n * fact(n - 1); }");
        Assert.Equal("156", machine.Start(host).Page!.Lines[0]);
    }

    [Fact]
    public void Functions_CanHoldAMenuAcrossASuspension()
    {
        var (machine, host) = Machine("let r = ask(); mes \"got \" + r; close;", "function ask() { return menu(\"one\", \"two\"); }");
        var menu = machine.Start(host);
        Assert.Equal(RunState.AwaitingMenu, menu.State);
        Lines(machine.Choose(host, 2).Page!.Lines, "got 2");
    }

    [Fact]
    public void Variables_ReadAndWriteEachScope()
    {
        var (machine, host) = Machine("char.visits += 1; account.gold = 5; server.count = char.visits + account.gold; mes server.count; close;");
        Assert.Equal("6", machine.Start(host).Page!.Lines[0]);
        Assert.Equal(1, host.GetVariable(VariableScope.Character, "visits").Number);
        Assert.Equal(5, host.GetVariable(VariableScope.Account, "gold").Number);
        Assert.Equal(6, host.GetVariable(VariableScope.Server, "count").Number);
    }

    [Fact]
    public void Builtins_ReachTheHost()
    {
        var (machine, host) = Machine(
            "givemoney(500); giveitem(1001, 3); if (takeitem(1001, 2)) { tell(\"took\"); } giveexp(10); givetm(4); warp(7, 30, 40); announce(\"hi \" + name());"
            + " mes countitem(1001) + \" \" + money() + \" \" + level() + \" \" + job() + \" \" + today(); close;");
        var result = machine.Start(host);
        Assert.Equal("1 500 42 2 20261022", result.Page!.Lines[0]);
        Lines(host.Told, "took");
        Assert.Equal(10, host.GrantedExperience);
        Assert.Equal(4, host.GrantedTechnique);
        Assert.Equal((7, 30, 40), Assert.Single(host.Warps));
        Assert.Equal("hi Tester", Assert.Single(host.Announced));
    }

    [Fact]
    public void QuestState_IsStoredAsACharacterVariable()
    {
        var (machine, host) = Machine("setqueststate(\"fetch\", 2); mes queststate(\"fetch\") + \" \" + questactive(5) + \" \" + questdone(6); close;");
        Assert.Equal("2 1 1", machine.Start(host).Page!.Lines[0]);
        Assert.Equal(2, host.GetVariable(VariableScope.Character, "quest.fetch").Number);
    }

    [Fact]
    public void NpcFunction_AndFallback_AreReported()
    {
        var (machine, host) = Machine("npcfunc(17); fallback;");
        var result = machine.Start(host);
        Assert.Equal(RunState.Fallback, result.State);
        Assert.Equal(17, Assert.Single(host.NpcFunctions));
    }

    [Fact]
    public void Arguments_AreReadableByIndex()
    {
        var (machine, host) = Machine("mes argc() + arg(1) + num(arg(2)) * 2; close;");
        host.Arguments.Add("x");
        host.Arguments.Add("21");
        Assert.Equal("2x42", machine.Start(host).Page!.Lines[0]);
    }

    [Fact]
    public void Budget_StopsAnInfiniteLoop()
    {
        var (machine, host) = Machine("while (1) { }", budget: 500);
        var error = Assert.Throws<ScriptRuntimeException>(() => machine.Start(host));
        Assert.StartsWith("test.tscript:", error.Message, StringComparison.Ordinal);
        Assert.Contains("500", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Recursion_IsLimited()
    {
        var (machine, host) = Machine("forever(); close;", "function forever() { forever(); }");
        Assert.Throws<ScriptRuntimeException>(() => machine.Start(host));
    }

    [Fact]
    public void DivisionByZero_NamesTheLine()
    {
        var (machine, host) = Machine("\n\nmes 1 / 0;");
        var error = Assert.Throws<ScriptRuntimeException>(() => machine.Start(host));
        Assert.StartsWith("test.tscript:3:", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PageLength_IsLimited()
    {
        var (machine, host) = Machine("let i = 0; while (i < 100) { mes \"x\"; i += 1; }");
        Assert.Throws<ScriptRuntimeException>(() => machine.Start(host));
    }

    [Fact]
    public void Compiler_RejectsAnUndeclaredVariable()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => ScriptCompiler.Compile("t.tscript", "function f() { x = 1; }"));
        Assert.Contains("let", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiler_RejectsAWrongBuiltinArity()
    {
        var error = Assert.Throws<ScriptSyntaxException>(() => ScriptCompiler.Compile("t.tscript", "function f() { warp(1, 2); }"));
        Assert.Contains("warp", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Compiler_RejectsBreakOutsideALoop()
    {
        Assert.Throws<ScriptSyntaxException>(() => ScriptCompiler.Compile("t.tscript", "function f() { break; }"));
    }

    [Fact]
    public void Compiler_RejectsARedeclaredVariable()
    {
        Assert.Throws<ScriptSyntaxException>(() => ScriptCompiler.Compile("t.tscript", "function f() { let a = 1; let a = 2; }"));
    }

    [Fact]
    public void NeedsPlayer_ErrorsWhenTheServerRunsAPlayerStep()
    {
        var file = ScriptCompiler.Compile("t.tscript", "every 10 seconds { tell(\"x\"); }");
        var machine = new ScriptMachine(file.Timers[0].Body, _ => null);
        var error = Assert.Throws<ScriptRuntimeException>(() => machine.Start(new ServerOnlyHost()));
        Assert.Contains("player", error.Message, StringComparison.Ordinal);
    }

    private sealed class ServerOnlyHost : ScriptHostBase
    {
        public override ScriptValue GetVariable(VariableScope scope, string name) => ScriptValue.Zero;

        public override void SetVariable(VariableScope scope, string name, ScriptValue value)
        {
        }
    }
}
