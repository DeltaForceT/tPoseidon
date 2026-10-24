using System.Runtime.CompilerServices;
using tPoseidon.Core.Game.Scripting.Compile;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Game.Scripting.Vm;

namespace tPoseidon.Core.GameTests.Scripting;

public class ExampleScriptTests
{
    private static readonly string ExamplesDirectory = Path.GetFullPath(Path.Combine(
        Path.GetDirectoryName(ThisFile())!, "..", "..", "..", "docs", "scripting", "examples"));

    private static string ThisFile([CallerFilePath] string path = "") => path;

    private static ScriptLibrary Library()
    {
        var result = ScriptLoader.LoadDirectory(ExamplesDirectory);
        Assert.Empty(result.Errors);
        return result.Library;
    }

    private static Routine Talk(ScriptLibrary library, string npcName) =>
        library.Npcs.Single(entry => entry.Npc.Declaration.Name == npcName).Npc.Talk;

    private static ScriptMachine Machine(ScriptLibrary library, Routine routine) => new(routine, library.FindFunction);

    [Fact]
    public void EveryExampleCompiles()
    {
        var result = ScriptLoader.LoadDirectory(ExamplesDirectory);
        Assert.Empty(result.Errors);
        Assert.Equal(4, result.FilesLoaded);
        Assert.Equal(3, result.Library.Npcs.Count);
    }

    [Fact]
    public void Greeter_CountsVisitsAndOffersTheMenu()
    {
        var library = Library();
        var host = new FakeScriptHost();
        var machine = Machine(library, Talk(library, "Welcome Guide"));
        var menu = machine.Start(host);
        Assert.Equal(RunState.AwaitingMenu, menu.State);
        Assert.Equal("Hello, Tester!", menu.Page!.Lines[0]);
        Assert.Equal("This is visit number 1.", menu.Page.Lines[1]);
        Assert.Equal(4, menu.Page.Options.Count);

        var gift = machine.Choose(host, 3);
        Assert.Equal(RunState.Closed, gift.State);
        Assert.Equal(500, host.Money);
    }

    [Fact]
    public void Greeter_HidesTheGiftOnceThePlayerIsRich()
    {
        var library = Library();
        var host = new FakeScriptHost();
        host.GiveMoney(5000);
        var menu = Machine(library, Talk(library, "Welcome Guide")).Start(host);
        Assert.DoesNotContain("Give me some money", menu.Page!.Options);
    }

    [Fact]
    public void Greeter_BankChoiceCallsTheOriginalFunction()
    {
        var library = Library();
        var host = new FakeScriptHost();
        var machine = Machine(library, Talk(library, "Welcome Guide"));
        machine.Start(host);
        machine.Choose(host, 2);
        Assert.Equal(17, Assert.Single(host.NpcFunctions));
    }

    [Fact]
    public void DailyReward_IsGivenOncePerDay()
    {
        var library = Library();
        var host = new FakeScriptHost();
        var routine = library.EventsFor(EventKind.Login).Single();
        Assert.True(ScriptRunner.TryRun(routine, library, host, _ => { }));
        Assert.Equal(1100, host.Money);
        Assert.Equal(1, host.Items[1001]);
        Assert.Single(host.Told);

        Assert.True(ScriptRunner.TryRun(routine, library, host, _ => { }));
        Assert.Equal(1100, host.Money);
        Assert.Single(host.Told);
        Assert.Equal(1, host.GetVariable(VariableScope.Character, "days_claimed").Number);
    }

    [Fact]
    public void FetchQuest_WalksThroughOfferCollectAndReward()
    {
        var library = Library();
        var host = new FakeScriptHost();
        var rina = Talk(library, "Herbalist Rina");

        var offer = Machine(library, rina).Start(host);
        Assert.Equal(RunState.AwaitingMenu, offer.State);
        var machine = Machine(library, rina);
        machine.Start(host);
        Assert.Equal(RunState.Closed, machine.Choose(host, 1).State);
        Assert.Equal(1, host.GetVariable(VariableScope.Character, "quest.herbs").Number);

        var notEnough = Machine(library, rina).Start(host);
        Assert.Equal(RunState.Closed, notEnough.State);
        Assert.Contains("0 of the 5", notEnough.Page!.Lines[0], StringComparison.Ordinal);

        host.Items[1002] = 5;
        var ready = Machine(library, rina);
        Assert.Equal(RunState.AwaitingNext, ready.Start(host).State);
        Assert.Equal(RunState.AwaitingMenu, ready.Continue(host).State);
        Assert.Equal(RunState.Closed, ready.Choose(host, 1).State);
        Assert.Equal(0, host.Items[1002]);
        Assert.Equal(2000, host.Money);
        Assert.Equal(500, host.GrantedExperience);
        Assert.Equal(2, host.GetVariable(VariableScope.Character, "quest.herbs").Number);

        var finished = Machine(library, rina).Start(host);
        Assert.Contains("Thank you again", finished.Page!.Lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void GmEvent_TurnsPlayersAwayAndLetsGameMastersControlTheEvent()
    {
        var library = Library();
        var player = new FakeScriptHost();
        var turnedAway = Machine(library, Talk(library, "Event Master")).Start(player);
        Assert.Equal("Only game masters can speak with me.", turnedAway.Page!.Lines[0]);

        var gm = new FakeScriptHost { GameMaster = true };
        var machine = Machine(library, Talk(library, "Event Master"));
        var menu = machine.Start(gm);
        Assert.Equal("Start the event", menu.Page!.Options[0]);
        machine.Choose(gm, 1);
        Assert.Equal(1, gm.GetVariable(VariableScope.Server, "event_running").Number);
        Assert.Contains("An event has started", Assert.Single(gm.Announced), StringComparison.Ordinal);
    }

    [Fact]
    public void GmEvent_CommandAndTimerShareTheSameState()
    {
        var library = Library();
        var gm = new FakeScriptHost { GameMaster = true };
        gm.Arguments.Add("start");
        var command = library.FindCommand("event")!.Value.Command;
        Assert.True(command.Declaration.GameMasterOnly);
        Assert.True(ScriptRunner.TryRun(command.Body, library, gm, _ => { }));
        Assert.Equal(1, gm.GetVariable(VariableScope.Server, "event_running").Number);

        var timer = library.Timers.Single().Timer.Body;
        Assert.True(ScriptRunner.TryRun(timer, library, gm, _ => { }));
        Assert.Equal(2, gm.Announced.Count);
    }
}
