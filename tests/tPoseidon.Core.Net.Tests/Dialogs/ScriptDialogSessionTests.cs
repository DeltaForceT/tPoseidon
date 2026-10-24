using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Scripting;
using tPoseidon.Core.Game.Scripting.Host;
using tPoseidon.Core.Game.Scripting.Lang;
using tPoseidon.Core.Game.Scripting.Library;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.Dialogs;

public class ScriptDialogSessionTests
{
    private const ushort Entity = 0x0300;

    private sealed class RecordingHost : ScriptHostBase, IScriptSessionHost
    {
        private readonly Dictionary<(VariableScope, string), ScriptValue> _variables = new();
        private readonly List<Func<CancellationToken, Task>> _effects = new();

        public List<string> Told { get; } = new();

        public bool CloseRequested { get; set; }

        public override bool HasPlayer => true;

        public override string PlayerName => "Tester";

        public override long Level => 30;

        public override long Money => 1000;

        public override ScriptValue GetVariable(VariableScope scope, string name) =>
            _variables.TryGetValue((scope, name), out var value) ? value : ScriptValue.Zero;

        public override void SetVariable(VariableScope scope, string name, ScriptValue value) => _variables[(scope, name)] = value;

        public override void GiveMoney(long amount) => Told.Add("money " + amount);

        public override void Tell(string text) => _effects.Add(_ =>
        {
            Told.Add(text);
            return Task.CompletedTask;
        });

        public override void RunNpcFunction(long functionId, string? table)
        {
            CloseRequested = true;
            Told.Add("function " + functionId);
        }

        public async Task FlushAsync(CancellationToken ct)
        {
            foreach (var effect in _effects) await effect(ct);
            _effects.Clear();
        }

        public bool TakeCloseRequest()
        {
            var requested = CloseRequested;
            CloseRequested = false;
            return requested;
        }
    }

    private sealed class Harness
    {
        public Harness(string source, Func<NpcEntity, ushort, CancellationToken, Task<bool>>? fallback = null)
        {
            var load = ScriptLoader.Load(new[] { new ScriptSource("t.tscript", source) });
            Assert.Empty(load.Errors);
            Library = load.Library;
            Session = new ScriptDialogSession(
                () => Library,
                Host,
                (opcode, payload, _) =>
                {
                    Frames.Add((opcode, payload));
                    return Task.CompletedTask;
                },
                fallback ?? ((_, _, _) => Task.FromResult(false)),
                Log.Add);
        }

        public ScriptLibrary Library { get; }

        public RecordingHost Host { get; } = new();

        public List<(Opcode Opcode, byte[] Payload)> Frames { get; } = new();

        public List<string> Log { get; } = new();

        public ScriptDialogSession Session { get; }

        public static NpcEntity CustomNpc(string name) => new()
        {
            EntityId = Entity,
            MapId = 1,
            ChrType = 1301,
            PosId = 0,
            Spawn = NpcPoint.Xy(120, 340),
            DisplayName = name,
            Source = new ScriptNpcSource(ScriptLibrary.NpcKey("t.tscript", name)),
        };

        public static NpcEntity OriginalNpc(int type, ushort map = 1, float x = 10, float y = 20) => new()
        {
            EntityId = Entity,
            MapId = map,
            ChrType = type,
            PosId = 0,
            Spawn = NpcPoint.Xy(x, y),
        };

        public NpcDialogPacket Opener() => NpcDialogPacket.DecodePayload(Frames.Single(frame => frame.Opcode == Opcode.S2C_NPC_DIALOG).Payload);

        public NpcMessagePage Followup(int index = -1)
        {
            var frame = Frames.Where(entry => entry.Opcode == Opcode.S2C_NPC_DIALOG_FOLLOWUP).ElementAt(index < 0 ? Frames.Count(entry => entry.Opcode == Opcode.S2C_NPC_DIALOG_FOLLOWUP) - 1 : index);
            return QuestFrames.ReadNpcMessage(frame.Payload, System.Text.Encoding.ASCII);
        }
    }

    private const string Greeter =
        "npc \"Mira\" sprite 1301 map 1 at 120, 340 { mes \"Hello, \" + name() + \"!\"; mes \"What do you need?\";"
        + " menu { \"Reward\" { givemoney(50); mes \"Here you go.\"; close; } \"Next page\" { mes \"One\"; next; mes \"Two\"; close; } \"Bye\" { end; } } }";

    [Fact]
    public async Task OpenAsync_SendsTheOpenerWithTextAndMenuLinks()
    {
        var harness = new Harness(Greeter);
        Assert.True(await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None));
        var opener = harness.Opener();
        Assert.Equal(Entity, opener.EntityId);
        Assert.Equal(1301, opener.PortraitType);
        Assert.Equal(ScriptDialogSession.OpenerRowCount, opener.NpcSType);
        Assert.Equal("Hello, Tester!<br/>What do you need?\\Reward\\1\\Next page\\2\\Bye\\3", opener.Text);
        Assert.True(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task HandlePageLinkAsync_RunsTheChosenBranchAndShowsTheClosePage()
    {
        var harness = new Harness(Greeter);
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        Assert.True(await harness.Session.HandlePageLinkAsync(Entity, 1, CancellationToken.None));
        var page = harness.Followup();
        Assert.Equal(Entity, page.NpcEntityId);
        Assert.Equal("Here you go.\\Close\\Exit", page.Text);
        Assert.Equal((ushort)0, page.Kind);
        Assert.True(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task ExitFunction_ClosesTheDialogWithTheOriginalCloseFrame()
    {
        var harness = new Harness(Greeter);
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        await harness.Session.HandlePageLinkAsync(Entity, 1, CancellationToken.None);
        Assert.True(await harness.Session.HandleFunctionAsync(Entity, 1, CancellationToken.None));
        Assert.Equal(Opcode.C2S_TRIGGER_75, harness.Frames[^1].Opcode);
        Assert.Empty(harness.Frames[^1].Payload);
        Assert.False(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task NextPages_AreContinuedByTheNextLink()
    {
        var harness = new Harness(Greeter);
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        await harness.Session.HandlePageLinkAsync(Entity, 2, CancellationToken.None);
        Assert.Equal("One\\Next\\1", harness.Followup().Text);
        await harness.Session.HandlePageLinkAsync(Entity, 1, CancellationToken.None);
        Assert.Equal("Two\\Close\\Exit", harness.Followup().Text);
    }

    [Fact]
    public async Task EndWithoutText_SendsTheCloseFrameAtOnce()
    {
        var harness = new Harness(Greeter);
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        await harness.Session.HandlePageLinkAsync(Entity, 3, CancellationToken.None);
        Assert.Equal(Opcode.C2S_TRIGGER_75, harness.Frames[^1].Opcode);
        Assert.False(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task EffectsRunBeforeTheNextPageIsShown()
    {
        var harness = new Harness("npc \"Mira\" sprite 1301 map 1 at 120, 340 { tell(\"a gift\"); mes \"Done\"; close; }");
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        Assert.Equal("a gift", Assert.Single(harness.Host.Told));
    }

    [Fact]
    public async Task OutOfRangeMenuChoices_AreIgnored()
    {
        var harness = new Harness(Greeter);
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        var before = harness.Frames.Count;
        await harness.Session.HandlePageLinkAsync(Entity, 9, CancellationToken.None);
        Assert.Equal(before, harness.Frames.Count);
        Assert.True(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task AnOverride_TakesTheTalkOfAnOriginalNpcByTypeMapAndPosition()
    {
        var harness = new Harness("override npc 77 map 1 at 10, 20 { mes \"Custom words\"; close; }");
        Assert.False(await harness.Session.OpenAsync(Harness.OriginalNpc(77, map: 2), Entity, CancellationToken.None));
        Assert.False(await harness.Session.OpenAsync(Harness.OriginalNpc(78), Entity, CancellationToken.None));
        Assert.Empty(harness.Frames);
        Assert.True(await harness.Session.OpenAsync(Harness.OriginalNpc(77), Entity, CancellationToken.None));
        Assert.Equal("Custom words\\Close\\Exit", harness.Opener().Text);
    }

    [Fact]
    public async Task Fallback_HandsTheNpcBackToTheOriginalDialog()
    {
        NpcEntity? handedBack = null;
        var harness = new Harness(
            "override npc 77 { mes \"Ignored\"; fallback; }",
            (npc, _, _) =>
            {
                handedBack = npc;
                return Task.FromResult(true);
            });
        var original = Harness.OriginalNpc(77);
        Assert.True(await harness.Session.OpenAsync(original, Entity, CancellationToken.None));
        Assert.Same(original, handedBack);
        Assert.Empty(harness.Frames);
        Assert.False(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task Fallback_ClosesTheDialogWhenTheOriginalHasNothingToShow()
    {
        var harness = new Harness("override npc 77 { fallback; }");
        await harness.Session.OpenAsync(Harness.OriginalNpc(77), Entity, CancellationToken.None);
        Assert.Equal(Opcode.C2S_TRIGGER_75, harness.Frames.Single().Opcode);
    }

    [Fact]
    public async Task ARuntimeError_IsLoggedWithFileAndLineAndClosesTheDialog()
    {
        var harness = new Harness("npc \"Mira\" sprite 1301 map 1 at 120, 340 {\n mes 1 / 0;\n}");
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        Assert.StartsWith("t.tscript:2:", Assert.Single(harness.Log), StringComparison.Ordinal);
        Assert.Equal(Opcode.C2S_TRIGGER_75, harness.Frames.Single().Opcode);
        Assert.False(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task AWindowFunction_EndsTheScriptDialogWithoutAFrame()
    {
        var harness = new Harness("npc \"Mira\" sprite 1301 map 1 at 120, 340 { npcfunc(17); mes \"unreached\"; close; }");
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        Assert.Empty(harness.Frames);
        Assert.False(harness.Session.IsEngaged);
    }

    [Fact]
    public async Task VariablesCarryStateBetweenPages()
    {
        var harness = new Harness(
            "npc \"Mira\" sprite 1301 map 1 at 120, 340 { char.visits += 1; mes \"Visit \" + char.visits; menu { \"Again\" { mes \"Count is \" + char.visits; close; } } }");
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        await harness.Session.HandlePageLinkAsync(Entity, 1, CancellationToken.None);
        Assert.Equal("Count is 1\\Close\\Exit", harness.Followup().Text);
    }

    [Fact]
    public void Markup_StripsSeparatorsAndNonAsciiFromScriptText()
    {
        var page = new tPoseidon.Core.Game.Scripting.Vm.ScriptPage(
            tPoseidon.Core.Game.Scripting.Vm.PageKind.Menu,
            new List<string> { "a\\b café" },
            new List<string> { "Opt\\ion" });
        Assert.Equal("ab caf?\\Option\\1", ScriptDialogMarkup.Render(page));
    }

    [Fact]
    public async Task AMenuOverTheLimit_IsRefusedInsteadOfSent()
    {
        var options = string.Join(" ", Enumerable.Range(1, ScriptDialogMarkup.MaximumMenuOptions + 1).Select(i => $"\"O{i}\" {{ end; }}"));
        var harness = new Harness("npc \"Mira\" sprite 1301 map 1 at 120, 340 { menu { " + options + " } }");
        await harness.Session.OpenAsync(Harness.CustomNpc("Mira"), Entity, CancellationToken.None);
        Assert.Single(harness.Log);
        Assert.Equal(Opcode.C2S_TRIGGER_75, harness.Frames.Single().Opcode);
    }
}
