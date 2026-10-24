using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Game.Scripting.Vars;

namespace tPoseidon.Core.GameTests.Scripting;

public class LibraryTests
{
    private static ScriptLoadResult Load(params (string File, string Text)[] files) =>
        ScriptLoader.Load(files.Select(file => new ScriptSource(file.File, file.Text)));

    [Fact]
    public void Load_CollectsEveryDeclarationKind()
    {
        var result = Load(("a.tscript",
            "npc \"Mira\" sprite 5 map 1 at 2, 3 { end; } override npc 9 { end; } on login { end; } every 10 seconds { end; }"
            + " command \"hi\" { end; } function f() { return 1; }"));
        Assert.Empty(result.Errors);
        Assert.Equal(1, result.FilesLoaded);
        Assert.Single(result.Library.Npcs);
        Assert.Equal(1, result.Library.OverrideCount);
        Assert.Equal(1, result.Library.EventCount);
        Assert.Single(result.Library.Timers);
        Assert.NotNull(result.Library.FindCommand("HI"));
        Assert.NotNull(result.Library.FindFunction("f"));
    }

    [Fact]
    public void Load_ReportsFileLineAndColumnAndKeepsGoodFiles()
    {
        var result = Load(
            ("good.tscript", "on login { end; }"),
            ("bad.tscript", "on login {\n  mes \"x\"\n}"));
        Assert.Equal(1, result.FilesLoaded);
        Assert.Equal(1, result.FilesFailed);
        var error = Assert.Single(result.Errors);
        Assert.StartsWith("bad.tscript:3:", error, StringComparison.Ordinal);
        Assert.Equal(1, result.Library.EventCount);
    }

    [Fact]
    public void Load_DropsAFileThatCallsAnUndefinedFunction()
    {
        var result = Load(("a.tscript", "on login { missing(); }"));
        Assert.Equal(0, result.FilesLoaded);
        Assert.Contains("missing", Assert.Single(result.Errors), StringComparison.Ordinal);
    }

    [Fact]
    public void Load_ResolvesFunctionsAcrossFiles()
    {
        var result = Load(
            ("lib.tscript", "function greet() { return 1; }"),
            ("use.tscript", "on login { greet(); }"));
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.FilesLoaded);
    }

    [Fact]
    public void Load_RejectsADuplicateFunctionAndACommandNameClash()
    {
        var result = Load(
            ("a.tscript", "function f() { return 1; } command \"x\" { end; }"),
            ("b.tscript", "function f() { return 2; }"),
            ("c.tscript", "command \"X\" { end; }"));
        Assert.Equal(1, result.FilesLoaded);
        Assert.Equal(2, result.Errors.Count);
    }

    [Fact]
    public void Load_RejectsABadCronAndAShortInterval()
    {
        var result = Load(
            ("a.tscript", "cron \"61 * * * *\" { end; }"),
            ("b.tscript", "every 1 seconds { end; }"),
            ("c.tscript", "at \"25:00\" { end; }"));
        Assert.Equal(0, result.FilesLoaded);
        Assert.Equal(3, result.Errors.Count);
    }

    [Fact]
    public void Load_RejectsAFunctionNamedLikeABuiltin()
    {
        var result = Load(("a.tscript", "function level() { return 1; }"));
        Assert.Single(result.Errors);
    }

    [Fact]
    public void FindOverride_PrefersTheMostSpecificMatch()
    {
        var result = Load(
            ("a.tscript", "override npc 7 { mes \"any\"; } override npc 7 map 2 { mes \"map\"; } override npc 7 map 2 at 10, 20 { mes \"exact\"; }"));
        var library = result.Library;
        Assert.Contains(library.FindOverride(7, 2, 10, 20)!.Constants, value => value.IsText && value.Text == "exact");
        Assert.Contains(library.FindOverride(7, 2, 11, 20)!.Constants, value => value.IsText && value.Text == "map");
        Assert.Contains(library.FindOverride(7, 9, 0, 0)!.Constants, value => value.IsText && value.Text == "any");
        Assert.Null(library.FindOverride(8, 2, 10, 20));
    }

    [Fact]
    public void FindOverride_RespectsMapAndPositionFilters()
    {
        var result = Load(("a.tscript", "override npc 7 map 2 at 10, 20 { end; }"));
        Assert.NotNull(result.Library.FindOverride(7, 2, 10, 20));
        Assert.Null(result.Library.FindOverride(7, 3, 10, 20));
        Assert.Null(result.Library.FindOverride(7, 2, 11, 20));
    }

    [Fact]
    public void EventsFor_FiltersByNumberAndName()
    {
        var result = Load(("a.tscript", "on mapenter { end; } on mapenter 5 { end; } on kill 9 { end; } on event \"boss\" { end; }"));
        var library = result.Library;
        Assert.Equal(2, library.EventsFor(EventKind.MapEnter, 5).Count());
        Assert.Single(library.EventsFor(EventKind.MapEnter, 6));
        Assert.Single(library.EventsFor(EventKind.MonsterKill, 9));
        Assert.Empty(library.EventsFor(EventKind.MonsterKill, 10));
        Assert.Single(library.EventsFor(EventKind.Named, name: "boss"));
        Assert.Empty(library.EventsFor(EventKind.Named, name: "other"));
    }

    [Fact]
    public void LoadDirectory_ReadsTscriptFilesRecursively()
    {
        var root = Path.Combine(Path.GetTempPath(), "tscript-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        try
        {
            File.WriteAllText(Path.Combine(root, "one.tscript"), "on login { end; }");
            File.WriteAllText(Path.Combine(root, "sub", "two.tscript"), "on levelup { end; }");
            File.WriteAllText(Path.Combine(root, "ignored.txt"), "not a script");
            var result = ScriptLoader.LoadDirectory(root);
            Assert.Equal(2, result.FilesLoaded);
            Assert.Empty(result.Errors);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectory_OfAMissingFolderIsEmpty()
    {
        var result = ScriptLoader.LoadDirectory(Path.Combine(Path.GetTempPath(), "tscript-missing-" + Guid.NewGuid().ToString("N")));
        Assert.Equal(0, result.FilesLoaded);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Cron_MatchesStepsRangesAndLists()
    {
        var cron = CronSchedule.Parse("*/15 8-10 * * 1,3");
        Assert.True(cron.Matches(new DateTimeOffset(2026, 10, 19, 9, 30, 0, TimeSpan.Zero)));
        Assert.False(cron.Matches(new DateTimeOffset(2026, 10, 19, 9, 31, 0, TimeSpan.Zero)));
        Assert.False(cron.Matches(new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero)));
        Assert.False(cron.Matches(new DateTimeOffset(2026, 10, 19, 11, 30, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Cron_RejectsWrongFieldCounts()
    {
        Assert.Throws<FormatException>(() => CronSchedule.Parse("* * * *"));
    }

    [Fact]
    public void Scheduler_FiresIntervalsDailyTimesAndCron()
    {
        var result = Load(("a.tscript", "every 10 seconds { end; } at \"20:30\" { end; } cron \"*/5 * * * *\" { end; }"));
        var start = new DateTimeOffset(2026, 10, 22, 20, 29, 30, TimeSpan.Zero);
        var scheduler = new ScriptScheduler(result.Library.Timers, start);
        Assert.Empty(scheduler.Collect(start.AddSeconds(5)));
        var tenSeconds = scheduler.Collect(start.AddSeconds(10));
        Assert.Single(tenSeconds);
        Assert.Equal(TimerKind.Interval, tenSeconds[0].Timer.Declaration.Kind);
        var minuteTick = scheduler.Collect(start.AddSeconds(31));
        Assert.Contains(minuteTick, entry => entry.Timer.Declaration.Kind == TimerKind.DailyAt);
        Assert.Contains(minuteTick, entry => entry.Timer.Declaration.Kind == TimerKind.Cron);
        Assert.DoesNotContain(scheduler.Collect(start.AddSeconds(35)), entry => entry.Timer.Declaration.Kind == TimerKind.DailyAt);
    }

    [Fact]
    public void Variables_PersistThroughTheStoreAndAreSeparatedByScopeAndOwner()
    {
        var store = new InMemoryScriptVariableStore();
        var variables = new ScriptVariables(store);
        variables.Set(VariableScope.Character, 7, "level_gift", ScriptValue.FromNumber(3));
        variables.Set(VariableScope.Account, 7, "level_gift", ScriptValue.FromText("account"));
        variables.Set(VariableScope.Server, ScriptVariables.ServerOwner, "event", ScriptValue.FromNumber(1));

        var reloaded = new ScriptVariables(store);
        Assert.Equal(3, reloaded.Get(VariableScope.Character, 7, "level_gift").Number);
        Assert.Equal("account", reloaded.Get(VariableScope.Account, 7, "level_gift").Text);
        Assert.Equal(1, reloaded.Get(VariableScope.Server, ScriptVariables.ServerOwner, "event").Number);
        Assert.Equal(0, reloaded.Get(VariableScope.Character, 8, "level_gift").Number);
    }

    [Fact]
    public void Variables_RejectTooManyOrOverlongNames()
    {
        var variables = new ScriptVariables(new InMemoryScriptVariableStore());
        Assert.Throws<tPoseidon.Core.Game.Scripting.Host.ScriptRuntimeException>(
            () => variables.Set(VariableScope.Character, 1, new string('n', ScriptVariables.MaximumNameLength + 1), ScriptValue.Zero));
        for (var i = 0; i < ScriptVariables.MaximumVariablesPerOwner; i++)
        {
            variables.Set(VariableScope.Character, 1, "v" + i, ScriptValue.FromNumber(i));
        }
        Assert.Throws<tPoseidon.Core.Game.Scripting.Host.ScriptRuntimeException>(
            () => variables.Set(VariableScope.Character, 1, "extra", ScriptValue.Zero));
        variables.Set(VariableScope.Character, 1, "v0", ScriptValue.FromNumber(99));
    }
}
