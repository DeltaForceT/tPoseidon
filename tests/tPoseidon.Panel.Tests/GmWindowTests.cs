using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel.Tests;

public sealed class GmWindowTests
{
    private const string Gm = "Boss";

    [AvaloniaFact]
    public void EveryGmCommandHasAPlaceInTheWindow()
    {
        using var window = new Owned(Services(new Transport(), new Accounts()));

        var expected = GameMasterToolCommands.All.Select(command => command.Name).Concat(GmPenalties.Names).Concat(GmAccounts.Names);

        Assert.Empty(expected.Except(window.Window.Model.Wired));
        Assert.Equal(["Players", "Spawn", "Server", "Events", "Guilds", "Penalties", "GM accounts", "Help"], window.Window.Tabs);
    }

    [Fact]
    public async Task CommandsNeedAConnectionFirst()
    {
        var transport = new Transport();
        using var model = new GmModel(Services(transport, new Accounts()));

        Assert.Null(await model.ToolAsync("Zone counts", "zone-counts", []));

        var entry = Assert.Single(model.History);
        Assert.Equal(GmEntryKind.Failed, entry.Kind);
        Assert.StartsWith("Connect first", entry.Lines[0], StringComparison.Ordinal);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task AConnectedModelRunsCommandsAndRecordsTheAnswers()
    {
        var transport = new Transport();
        using var model = await ConnectedAsync(transport);
        Assert.Equal(Gm, model.GameMaster);

        transport.Replies = [new ToolReply(GameMasterFrames.LoginOk, []), new ToolReply(GameMasterFrames.KickFail, [])];
        await model.ToolAsync("Kick", "kick", ["Friend"]);
        var refused = model.History[0];
        Assert.Equal((GmEntryKind.Refused, "kick failed: not online"), (refused.Kind, Assert.Single(refused.Lines)));
        Assert.Equal(Gm, transport.Sent[^1].Login.Character);

        transport.Replies = [new ToolReply(GameMasterFrames.LoginOk, [])];
        await model.ToolAsync("Memo", "memo", ["Friend", "Hello"]);
        var sent = model.History[0];
        Assert.Equal(GmEntryKind.Done, sent.Kind);
        Assert.StartsWith("Sent.", Assert.Single(sent.Lines), StringComparison.Ordinal);
        Assert.False(model.Busy);
    }

    [Fact]
    public async Task WrongArgumentsAreRefusedBeforeAnythingIsSent()
    {
        var transport = new Transport();
        using var model = await ConnectedAsync(transport);

        await model.ToolAsync("Speed", "speed", ["fast"]);

        Assert.Equal(GmEntryKind.Failed, model.History[0].Kind);
        Assert.Contains("Usage: tposeidon gm speed", model.History[0].Lines[0], StringComparison.Ordinal);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task ALostServerIsExplainedAndDisconnects()
    {
        var transport = new Transport();
        using var model = await ConnectedAsync(transport);

        transport.Replies = [];
        transport.Listening = false;
        await model.ToolAsync("Zone counts", "zone-counts", []);

        Assert.Equal(GmEntryKind.Failed, model.History[0].Kind);
        Assert.StartsWith("No answer", model.History[0].Lines[0], StringComparison.Ordinal);
        Assert.Equal(GmLinkStatus.ServerDown, model.Link.Status);
        Assert.Null(model.GameMaster);
    }

    [Fact]
    public async Task ConnectingWithoutARegisteredGmSaysSo()
    {
        using var model = new GmModel(Services(new Transport(), new Accounts { Names = [] }));

        await model.ConnectAsync(Gm, "secret");

        Assert.Equal(GmLinkStatus.NoGameMaster, model.Link.Status);
        Assert.Null(model.GameMaster);
    }

    [Fact]
    public async Task PenaltyAndAccountCommandsUseTheSharedLayer()
    {
        using var model = new GmModel(Services(new Transport(), new Accounts()));

        await model.PenaltyAsync("Penalties", GmPenalties.ListName, []);
        Assert.Equal(["No penalty kinds.", "No running penalties."], model.History[0].Lines);

        await model.AccountsAsync("List", GmAccounts.List);
        Assert.Equal("GM characters:", model.History[0].Lines[0]);
    }

    [Fact]
    public void AnUnconfiguredPanelExplainsWhy()
    {
        using var model = new GmModel(GmServices.For(new InstallPaths(Path.Combine(Path.GetTempPath(), "tposeidon-gm-" + Guid.NewGuid().ToString("N")))));

        Assert.Equal(GmLinkStatus.NotSetUp, model.Link.Status);
        Assert.Contains("not set up", model.Link.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AButtonWithAnEmptyFieldMarksItAndSendsNothing()
    {
        var transport = new Transport();
        using var window = new Owned(Services(transport, new Accounts()));
        var lookUp = window.Window.GetLogicalDescendants().OfType<Button>().First(button => button.Content as string == "Look up");
        var name = window.Window.GetLogicalDescendants().OfType<TextBox>().First(box => box.Watermark == "Character name");

        lookUp.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("bad", name.Classes);
        Assert.StartsWith("Check the marked fields.", window.Window.Model.History[0].Lines[0], StringComparison.Ordinal);
        Assert.Empty(transport.Sent);
    }

    [AvaloniaFact]
    public async Task TheHelpTabListsTheChatCommands()
    {
        using var window = new Owned(Services(new Transport(), new Accounts()));
        window.Window.Show();
        window.Window.ShowTab(window.Window.Tabs.Count - 1);
        await window.Window.Model.CatalogAsync();
        Dispatcher.UIThread.RunJobs();

        var texts = window.Window.GetLogicalDescendants().OfType<SelectableTextBlock>().Select(block => block.Text ?? "").ToList();

        Assert.Contains(texts, text => text.StartsWith(GameMasterChat.Login + " (no keyword", StringComparison.Ordinal));
        Assert.Contains(texts, text => text.StartsWith("summon-monster <monsterType> [count]", StringComparison.Ordinal));
    }

    private static async Task<GmModel> ConnectedAsync(Transport transport)
    {
        var model = new GmModel(Services(transport, new Accounts()));
        await model.ConnectAsync(Gm, "secret");
        Assert.True(model.Link.Connected);
        return model;
    }

    private static GmServices Services(Transport transport, Accounts accounts) => new()
    {
        Transport = transport,
        Endpoint = new GmToolEndpoint(GmToolEndpoint.LocalHost, 13336),
        Accounts = accounts,
        Penalties = new Penalties(),
        Catalog = () => GameCatalog.Empty("No game data in the tests."),
    };

    private sealed class Owned : IDisposable
    {
        public Owned(GmServices services)
        {
            Window = new GmWindow(new GmModel(services));
        }

        public GmWindow Window { get; }

        public void Dispose() => Window.Close();
    }

    private sealed class Transport : IGmToolTransport
    {
        public List<(GameMasterToolRequest Request, GmLogin Login)> Sent { get; } = [];

        public IReadOnlyList<ToolReply> Replies { get; set; } = [new ToolReply(GameMasterFrames.LoginOk, [])];

        public bool? Listening { get; set; } = true;

        public Task<IReadOnlyList<ToolReply>> SendAsync(GmToolEndpoint endpoint, GmLogin login, GameMasterToolRequest request, bool expectsReply, CancellationToken ct)
        {
            Sent.Add((request, login));
            return Task.FromResult(Replies);
        }

        public Task<bool> LoginAsync(GmToolEndpoint endpoint, GmLogin login, CancellationToken ct) => Task.FromResult(Listening != false);

        public bool? IsListening(GmToolEndpoint endpoint) => Listening;
    }

    private sealed class Accounts : IGmAccountBook
    {
        public IReadOnlyList<string> Names { get; set; } = [Gm];

        public GmCharacterRegistration? Register(string characterName, string permittedIp) => new(1, characterName, permittedIp);

        public GmCharacterEntry? Unregister(string characterName) => null;

        public IReadOnlyList<GmCharacterEntry> List() => Names.Select(name => new GmCharacterEntry(1, "owner", name, [GmAccounts.LocalPermitIp], DateTimeOffset.UnixEpoch)).ToList();

        public IReadOnlyList<CharacterSummary> FindCharacters(string text, int limit) => [];
    }

    private sealed class Penalties : IGmPenaltyBook
    {
        public void SetKind(PenaltyKind kind)
        {
        }

        public void SetTime(short code, short count, int hours)
        {
        }

        public IReadOnlyList<PenaltyKind> Kinds() => [];

        public int Penalize(string accountName, short code, long gameMasterAccountId, string note) => PenaltyResults.Ok;

        public int Release(string accountName, string gameMasterName) => 0;

        public IReadOnlyList<RunningPenalty> Running() => [];
    }
}
