using System.Net;
using System.Net.Sockets;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;
using tPoseidon.Kr2014.SystemServer;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterChatLinkTests
{
    private const ulong Gm = 0x10002051;
    private const ulong Player = 0x10002052;
    private const ushort Zone = 148;
    private const string GmName = "Keeper";
    private const string PlayerName = "Mule";
    private const string Password = "dummy";
    private const string Loopback = "127.0.0.1";

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class Authority : IGmAuthorityStore
    {
        public bool IsGmClientLoginPermitted(string characterName, string password, string clientIp) =>
            characterName == GmName && password == Password && clientIp == Loopback;

        public GmCharacterRegistration? RegisterGmCharacter(string characterName, string permittedIp) => throw new NotSupportedException();

        public ulong? GmCharacterId(string characterName) => characterName == GmName ? Gm : null;
    }

    private sealed class NoRateEvents : IGmRateEventStore
    {
        public GmRateEventState Load() => GmRateEventState.Empty;

        public void Save(GmRateEventState state)
        {
        }
    }

    private sealed class Rig : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        private readonly List<Task> _running = [];
        private S2SessionContext? _gameSide;

        public SystemServerLink Game { get; } = new(_ => true, _ => { });

        public GameMasterChatRelay Relay { get; }

        public List<string> SystemLog { get; } = [];

        public List<string> Replies { get; } = [];

        public List<OfficerOrder> Orders { get; } = [];

        public Rig()
        {
            Relay = new GameMasterChatRelay(Game, Text);
            Game.OrderReceived += (order, _) =>
            {
                lock (Orders) Orders.Add(order);
                return Task.CompletedTask;
            };
            Game.MessageReceived += (opcode, payload, token) =>
            {
                if (opcode == GameMasterFrames.LinkChatReply && GameMasterFrames.TryReadChatLine(payload.Span, Text, out _, out var line))
                {
                    lock (Replies) Replies.Add(line);
                }
                return Task.CompletedTask;
            };
        }

        public async Task StartAsync()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var keyTable = S2ServerHost.DefaultKeyTablePath();
            var host = new S2ServerHost
            {
                LogTag = "[test-game]",
                Port = port,
                KeyTablePath = keyTable,
                BindAddress = IPAddress.Loopback,
                OnSessionAccepted = (ctx, _) =>
                {
                    Game.Register(ctx);
                    _gameSide = ctx;
                    return Task.CompletedTask;
                },
            };
            _running.Add(host.RunAsync(_stop.Token));
            await host.Listening;

            var links = new GameServerLinks(keyTable, line => { lock (SystemLog) SystemLog.Add(line); });
            var server = new GameMasterServer(
                new GameMasterService(new Authority(), Text),
                new GameMasterChat(new Dictionary<string, string> { [GameMasterChat.GmFlagSetText] = "GM mode on." }),
                new Authority(),
                new SocialRoom(),
                links,
                new NoRateEvents(),
                Text,
                true,
                line => { lock (SystemLog) SystemLog.Add(line); });
            links.OnGameMasterChat = server.ChatAsync;
            _running.Add(links.RunAsync(Loopback, port, _stop.Token));

            await Until(() => Game.IsConnected);
            foreach (var character in new[] { Gm, Player })
            {
                await Game.EnteredZoneAsync(_gameSide!, character, Zone, _stop.Token);
                await Until(() => links.ZoneOf(character) == Zone);
            }
        }

        public async Task<GameMasterRelay> SayAsync(ulong characterId, string name, string clientLine)
        {
            var payload = Encoding.ASCII.GetBytes(clientLine + "\0");
            return await Relay.RelayAsync(characterId, name, Loopback, payload, _stop.Token);
        }

        public async Task<string> NextReplyAsync(int alreadySeen)
        {
            await Until(() => { lock (Replies) return Replies.Count > alreadySeen; });
            lock (Replies) return Replies[alreadySeen];
        }

        public async Task<OfficerOrder> OrderAsync(ushort orderType)
        {
            await Until(() => { lock (Orders) return Orders.Any(order => order.OrderType == orderType); });
            lock (Orders) return Orders.Last(order => order.OrderType == orderType);
        }

        public bool Logged(string fragment)
        {
            lock (SystemLog) return SystemLog.Any(line => line.Contains(fragment, StringComparison.Ordinal));
        }

        public string[] LogSnapshot()
        {
            lock (SystemLog) return SystemLog.ToArray();
        }

        public int OrderCount()
        {
            lock (Orders) return Orders.Count;
        }

        private static async Task Until(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (!condition())
            {
                Assert.True(DateTime.UtcNow < deadline, "the condition was not reached in time");
                await Task.Delay(20);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync();
            try
            {
                await Task.WhenAll(_running).WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (ex is OperationCanceledException or TimeoutException or IOException or SocketException)
            {
            }
            _stop.Dispose();
        }
    }

    [Fact]
    public async Task TheClientsLoginLineLogsTheGmInSetsTheEagleAndALaterCommandWorks()
    {
        await using var rig = new Rig();
        await rig.StartAsync();

        var login = await rig.SayAsync(Gm, GmName, "@login " + Password);

        Assert.Equal(GameMasterRelayResult.Relayed, login.Result);
        var eagle = await rig.OrderAsync(GameMasterFrames.OrderSetGmAttribute);
        Assert.Equal(Gm, eagle.CharacterId);
        Assert.True(GameMasterFrames.TryReadGmAttribute(eagle.Data, out var on) && on);
        Assert.Equal("GM mode on.", await rig.NextReplyAsync(0));

        var speed = await rig.SayAsync(Gm, GmName, "@speed 3");

        Assert.Equal(GameMasterRelayResult.Relayed, speed.Result);
        var run = await rig.OrderAsync(GameMasterFrames.OrderSuperRun);
        Assert.True(GameMasterFrames.TryReadSuperRun(run.Data, out var factor));
        Assert.Equal(3u, factor);
        Assert.True(rig.Logged("tries @login"));
        Assert.True(rig.Logged("@speed"));
    }

    [Fact]
    public async Task ALoginWithASpaceAfterTheAtSignWorksToo()
    {
        await using var rig = new Rig();
        await rig.StartAsync();

        await rig.SayAsync(Gm, GmName, "@ login " + Password);

        Assert.True(GameMasterFrames.TryReadGmAttribute((await rig.OrderAsync(GameMasterFrames.OrderSetGmAttribute)).Data, out var on) && on);
    }

    [Fact]
    public async Task AnUnknownAtCommandAndARefusedLoginEachGetAPrivateReply()
    {
        await using var rig = new Rig();
        await rig.StartAsync();

        await rig.SayAsync(Gm, GmName, "@" + Password);
        Assert.Equal(GameMasterChat.UnknownCommandLine, await rig.NextReplyAsync(0));

        await rig.SayAsync(Player, PlayerName, "@login " + Password);
        Assert.Equal(GameMasterChat.NotAllowedLine, await rig.NextReplyAsync(1));

        await rig.SayAsync(Gm, GmName, "@login wrong");
        Assert.Equal(GameMasterChat.NotAllowedLine, await rig.NextReplyAsync(2));
        Assert.Equal(0, rig.OrderCount());
    }

    [Fact]
    public async Task EveryGmLineIsLoggedWithItsOutcomeAndNoPasswordOrRawTextReachesTheLog()
    {
        await using var rig = new Rig();
        await rig.StartAsync();

        await rig.SayAsync(Gm, GmName, "@login wrongpass");
        await rig.NextReplyAsync(0);
        await rig.SayAsync(Gm, GmName, "@" + Password);
        await rig.NextReplyAsync(1);
        await rig.SayAsync(Gm, GmName, "@login " + Password);
        await rig.OrderAsync(GameMasterFrames.OrderSetGmAttribute);

        var log = rig.LogSnapshot();
        Assert.Contains(log, line => line.Contains("refused", StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("unknown", StringComparison.Ordinal));
        Assert.Contains(log, line => line.Contains("tries @login", StringComparison.Ordinal));
        Assert.DoesNotContain(log, line => line.Contains("wrongpass", StringComparison.Ordinal) || line.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALineThatDoesNotStartWithAtIsNotRelayed()
    {
        await using var rig = new Rig();
        await rig.StartAsync();

        var relay = await rig.SayAsync(Gm, GmName, "login " + Password);

        Assert.Equal(GameMasterRelayResult.NotAGameMasterLine, relay.Result);
    }

    [Fact]
    public async Task WithoutASystemServerLinkTheLineIsReportedUndelivered()
    {
        var link = new SystemServerLink(_ => true, _ => { });
        var relay = new GameMasterChatRelay(link, Text);

        var result = await relay.RelayAsync(Gm, GmName, Loopback, Encoding.ASCII.GetBytes("@login " + Password + "\0"), CancellationToken.None);

        Assert.Equal(GameMasterRelayResult.LinkDown, result.Result);
    }
}
