using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Vars;
using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Tests.Dialogs;

public sealed class ScriptEngineTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "script-engine-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _log = new();

    public ScriptEngineTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private ScriptEngine Engine() => new(_directory, _log.Add);

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_directory, name), text);

    private sealed class Host : ScriptHostBase, IScriptSessionHost
    {
        private readonly Dictionary<(VariableScope, string), ScriptValue> _variables = new();

        public List<string> Told { get; } = new();

        public override bool HasPlayer => true;

        public override ScriptValue GetVariable(VariableScope scope, string name) =>
            _variables.TryGetValue((scope, name), out var value) ? value : ScriptValue.Zero;

        public override void SetVariable(VariableScope scope, string name, ScriptValue value) => _variables[(scope, name)] = value;

        public override void Tell(string text) => Told.Add(text);

        public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

        public bool TakeCloseRequest() => false;
    }

    [Fact]
    public void Reload_LogsTheCountsAndEveryError()
    {
        Write("good.tscript", "on login { end; }");
        Write("bad.tscript", "on login { mes }");
        var result = Engine().Reload();
        Assert.Equal(1, result.FilesLoaded);
        Assert.Contains(_log, line => line.Contains("loaded 1 file(s)", StringComparison.Ordinal));
        Assert.Contains(_log, line => line.StartsWith("[scripts] bad.tscript:1:", StringComparison.Ordinal));
    }

    [Fact]
    public void SyncNpcs_InstallsMovesAndRemovesScriptNpcs()
    {
        Write("mira.tscript", "npc \"Mira\" sprite 1301 map 1 at 120, 340 face east { end; }");
        var engine = Engine();
        engine.Reload();
        var store = new WorldEntityStore();
        var ids = new IdAllocator(0x4000, 0x40FF);
        var placed = new Dictionary<string, NpcEntity>();

        var first = engine.SyncNpcs(store, ids, placed);
        var npc = Assert.Single(first.Added);
        Assert.Equal("Mira", npc.DisplayName);
        Assert.Equal(1301, npc.ChrType);
        Assert.Equal(1, npc.MapId);
        Assert.Equal(120, (int)npc.Spawn.X);
        Assert.Equal(340, (int)npc.Spawn.Y);
        Assert.Equal(90, (int)npc.Spawn.Orientation);
        Assert.IsType<ScriptNpcSource>(npc.Source);
        Assert.Same(npc, store.Get(npc.EntityId));

        var second = engine.SyncNpcs(store, ids, placed);
        Assert.Empty(second.Added);
        Assert.Empty(second.Removed);

        Write("mira.tscript", "npc \"Mira\" sprite 1301 map 1 at 130, 340 { end; }");
        engine.Reload();
        var moved = engine.SyncNpcs(store, ids, placed);
        Assert.Single(moved.Removed);
        Assert.Single(moved.Added);
        Assert.Equal(130, (int)moved.Added[0].Spawn.X);
        Assert.Single(store.InMap(1));

        File.Delete(Path.Combine(_directory, "mira.tscript"));
        engine.Reload();
        var gone = engine.SyncNpcs(store, ids, placed);
        Assert.Single(gone.Removed);
        Assert.Empty(store.InMap(1));
    }

    [Fact]
    public void ConsumeReloadRequest_RemovesTheMarkerFile()
    {
        var engine = Engine();
        Assert.False(engine.ConsumeReloadRequest());
        File.WriteAllText(Path.Combine(_directory, ScriptEngine.ReloadRequestFile), string.Empty);
        Assert.True(engine.ConsumeReloadRequest());
        Assert.False(File.Exists(Path.Combine(_directory, ScriptEngine.ReloadRequestFile)));
    }

    [Fact]
    public void TryParseChatCommand_SplitsQuotedArguments()
    {
        Assert.True(ScriptEngine.TryParseChatCommand("@event start \"double exp\" 5", out var name, out var arguments));
        Assert.Equal("event", name);
        Assert.Equal(new List<string> { "start", "double exp", "5" }, arguments);
        Assert.False(ScriptEngine.TryParseChatCommand("hello @there", out _, out _));
        Assert.False(ScriptEngine.TryParseChatCommand("@", out _, out _));
    }

    [Fact]
    public async Task RunCommandAsync_RefusesAGameMasterCommandForPlayers()
    {
        Write("cmd.tscript", "command \"bonus\" gm { server.bonus = 1; } command \"hello\" { tell(\"hi \" + arg(1)); }");
        var engine = Engine();
        engine.Reload();
        var told = new List<string>();
        var host = new Host();

        Assert.True(await engine.RunCommandAsync("bonus", Array.Empty<string>(), isGameMaster: false, _ => host, (text, _) => { told.Add(text); return Task.CompletedTask; }, CancellationToken.None));
        Assert.Equal("That command is for game masters only.", Assert.Single(told));
        Assert.Equal(0, host.GetVariable(VariableScope.Server, "bonus").Number);

        Assert.True(await engine.RunCommandAsync("BONUS", Array.Empty<string>(), isGameMaster: true, _ => host, (_, _) => Task.CompletedTask, CancellationToken.None));
        Assert.Equal(1, host.GetVariable(VariableScope.Server, "bonus").Number);

        Assert.False(await engine.RunCommandAsync("unknown", Array.Empty<string>(), isGameMaster: true, _ => host, (_, _) => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task RunEventAsync_RunsEveryMatchingHookAndLogsErrorsWithoutStoppingTheOthers()
    {
        Write("events.tscript", "on mapenter { 1 / 0; } on mapenter 5 { tell(\"entered five\"); } on mapenter 6 { tell(\"entered six\"); }");
        var engine = Engine();
        engine.Reload();
        var host = new Host();
        await engine.RunEventAsync(EventKind.MapEnter, 5, null, host, CancellationToken.None);
        Assert.Equal("entered five", Assert.Single(host.Told));
        Assert.Contains(_log, line => line.StartsWith("events.tscript:1:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunTimersAsync_RunsDueTimersWithAServerHost()
    {
        Write("timers.tscript", "every 10 seconds { server.ticks += 1; announce(\"tick \" + server.ticks); }");
        var engine = Engine();
        engine.Reload();
        var announced = new List<string>();
        var variables = new ScriptVariables(new InMemoryScriptVariableStore());
        var world = new ScriptWorldServices
        {
            Variables = variables,
            Announce = (text, _) => { announced.Add(text); return Task.CompletedTask; },
            OnlinePlayers = () => 3,
            TriggerEvent = _ => { },
        };
        await engine.RunTimersAsync(world, DateTimeOffset.Now.AddSeconds(30), CancellationToken.None);
        Assert.Equal("tick 1", Assert.Single(announced));
        Assert.Equal(1, variables.Get(VariableScope.Server, ScriptVariables.ServerOwner, "ticks").Number);
    }
}
