using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterChatRouterTests
{
    private const ulong Gm = 0x4001;
    private const ulong OtherGm = 0x4002;
    private const ulong Player = 0x5001;
    private const ulong Friend = 0x5002;
    private const string GmName = "Keeper";
    private const string OtherGmName = "Warden";
    private const string PlayerName = "Mule";
    private const string Password = "secret";
    private const string OtherPassword = "hunter2";
    private const string Loopback = "127.0.0.1";
    private const string Elsewhere = "203.0.113.9";

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class Authority : IGmAuthorityStore
    {
        public Dictionary<string, ulong> Registered { get; } = new(StringComparer.OrdinalIgnoreCase) { [GmName] = Gm, [OtherGmName] = OtherGm };

        public bool IsGmClientLoginPermitted(string characterName, string password, string clientIp) =>
            clientIp == Loopback
            && ((characterName == GmName && password == Password) || (characterName == OtherGmName && password == OtherPassword));

        public GmCharacterRegistration? RegisterGmCharacter(string characterName, string permittedIp) => throw new NotSupportedException();

        public ulong? GmCharacterId(string characterName) => Registered.TryGetValue(characterName, out var id) ? id : null;
    }

    private sealed class Setup
    {
        public Authority Authority { get; } = new();

        public GameMasterService Service { get; }

        public GameMasterChatRouter Router { get; }

        public Setup()
        {
            Service = new GameMasterService(Authority, Text);
            Router = new GameMasterChatRouter(
                Service,
                Authority,
                Text,
                name => name == "Friend" ? Friend : null,
                id => id == Friend ? "Friend" : null,
                () => "10:00:00",
                () => ["Help_Login"])
            {
                InstantChatRemover = (_, _) => new GameMasterOutbox(),
            };
        }

        public GameMasterChatOutcome? Say(string line, string ip = Loopback) => Router.Route(Gm, GmName, ip, line);

        public GameMasterChatOutcome? LoggedInSay(string line)
        {
            Assert.NotNull(Say("@login " + Password)!.Outbox);
            return Say(line);
        }
    }

    [Fact]
    public void ARegisteredGmWithTheRightPasswordFromThePermittedAddressLogsIn()
    {
        var setup = new Setup();

        var outcome = setup.Say("@login " + Password)!;

        Assert.Null(outcome.Reply);
        Assert.NotNull(outcome.Outbox);
        Assert.True(setup.Service.IsLoggedIn(Gm));
        Assert.Contains(outcome.Outbox!.Orders, order => order.OrderType == GameMasterFrames.OrderSetGmAttribute);
        Assert.Contains(outcome.Outbox.Messages, message => message.Opcode == GameMasterFrames.LoginOk);
    }

    [Fact]
    public void EveryFailedLoginGetsTheSameGenericRefusalAndKicksNobody()
    {
        var setup = new Setup();
        var refusals = new List<GameMasterChatOutcome?>
        {
            setup.Router.Route(Player, PlayerName, Loopback, "@login " + Password),
            setup.Router.Route(Player, GmName, Loopback, "@login " + Password),
            setup.Router.Route(Gm, GmName, Loopback, "@login " + OtherPassword),
            setup.Router.Route(Gm, GmName, Loopback, "@login wrong"),
            setup.Router.Route(Gm, GmName, Elsewhere, "@login " + Password),
            setup.Router.Route(Gm, GmName, Loopback, "@login"),
            setup.Router.Route(Gm, GmName, Loopback, "@login " + Password + " extra"),
        };

        Assert.All(refusals, outcome =>
        {
            Assert.NotNull(outcome);
            Assert.Equal(GameMasterChat.NotAllowedLine, outcome!.Reply);
            Assert.Null(outcome.Outbox);
        });
        Assert.False(setup.Service.IsLoggedIn(Gm));
        Assert.False(setup.Service.IsLoggedIn(Player));
    }

    [Fact]
    public void AGmCharacterOfAnotherAccountCannotLogInWithThisAccountsPassword()
    {
        var setup = new Setup();

        var outcome = setup.Router.Route(OtherGm, OtherGmName, Loopback, "@login " + Password)!;

        Assert.Equal(GameMasterChat.NotAllowedLine, outcome.Reply);
        Assert.False(setup.Service.IsLoggedIn(OtherGm));
    }

    [Fact]
    public void TheAuditLinesNameTheCharacterAndAddressButNeverThePassword()
    {
        var setup = new Setup();

        var failed = setup.Say("@login wrongpass")!;
        var succeeded = setup.Say("@login " + Password)!;
        var command = setup.Say("@kick Friend")!;

        Assert.DoesNotContain(failed.Audit.Concat(succeeded.Audit), line => line.Contains("wrongpass", StringComparison.Ordinal) || line.Contains(Password, StringComparison.Ordinal));
        Assert.Contains(failed.Audit, line => line.Contains(GmName, StringComparison.Ordinal) && line.Contains(Loopback, StringComparison.Ordinal) && line.Contains("refused", StringComparison.Ordinal));
        Assert.Contains(succeeded.Audit, line => line.Contains("tries @login", StringComparison.Ordinal));
        Assert.Contains(command.Audit, line => line.Contains("@kick Friend", StringComparison.Ordinal) && line.Contains(GmName, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("@kick Friend")]
    [InlineData("@speed 3")]
    [InlineData("@supernotice \"hello\"")]
    [InlineData("@block Friend 3")]
    [InlineData("@logout")]
    [InlineData("@commandhelp")]
    public void ANormalPlayerTypingAnyGmKeywordIsToldNoAndNothingHappens(string line)
    {
        var setup = new Setup();

        var outcome = setup.Router.Route(Player, PlayerName, Loopback, line)!;

        Assert.Equal(GameMasterChat.NotAllowedLine, outcome.Reply);
        Assert.Null(outcome.Outbox);
    }

    [Fact]
    public void ARegisteredGmWhoDidNotLogInIsRefusedToo()
    {
        var setup = new Setup();

        var outcome = setup.Say("@kick Friend")!;

        Assert.Equal(GameMasterChat.NotAllowedLine, outcome.Reply);
        Assert.Null(outcome.Outbox);
    }

    [Fact]
    public void ACharacterThatLostItsGmRegistrationAfterLoginIsRefusedAndLoggedOut()
    {
        var setup = new Setup();
        Assert.NotNull(setup.Say("@login " + Password)!.Outbox);
        setup.Authority.Registered.Remove(GmName);

        var outcome = setup.Say("@kick Friend")!;

        Assert.Equal(GameMasterChat.NotAllowedLine, outcome.Reply);
        Assert.False(setup.Service.IsLoggedIn(Gm));
        Assert.Contains(outcome.Outbox!.Orders, order => order.OrderType == GameMasterFrames.OrderSetGmAttribute);
    }

    [Fact]
    public void LoggingOutClearsTheGmFlagAndTheCommandsStopWorking()
    {
        var setup = new Setup();

        var logout = setup.LoggedInSay("@logout")!;

        Assert.Contains(logout.Outbox!.Orders, order => order.OrderType == GameMasterFrames.OrderSetGmAttribute);
        Assert.False(setup.Service.IsLoggedIn(Gm));
        Assert.Equal(GameMasterChat.NotAllowedLine, setup.Say("@kick Friend")!.Reply);
    }

    [Fact]
    public void AnUnknownAtCommandIsToldSoAndItsWordsAreNeverLogged()
    {
        var setup = new Setup();

        var outcome = setup.Say("@" + Password)!;

        Assert.Equal(GameMasterChat.UnknownCommandLine, outcome.Reply);
        Assert.Null(outcome.Outbox);
        Assert.Contains(outcome.Audit, line => line.Contains(GmName, StringComparison.Ordinal) && line.Contains("unknown", StringComparison.Ordinal));
        Assert.DoesNotContain(outcome.Audit, line => line.Contains(Password, StringComparison.Ordinal));
    }

    [Fact]
    public void ALineThatDoesNotStartWithAtIsLeftAlone()
    {
        var setup = new Setup();

        Assert.Null(setup.Say("/login " + Password));
        Assert.Null(setup.Say("login " + Password));
        Assert.Null(setup.Say("@"));
    }

    [Fact]
    public void AKeywordWithTheWrongArgumentsNeverLogsTheArguments()
    {
        var setup = new Setup();

        var outcome = setup.LoggedInSay("@speed " + OtherPassword)!;

        Assert.Equal(GameMasterChat.BadArgumentsLine, outcome.Reply);
        Assert.DoesNotContain(outcome.Audit, line => line.Contains(OtherPassword, StringComparison.Ordinal));
        Assert.Contains(outcome.Audit, line => line.Contains("not understood", StringComparison.Ordinal));
    }

    [Fact]
    public void ALoggedInGmWithTheWrongArgumentsIsToldSo()
    {
        var setup = new Setup();

        var outcome = setup.LoggedInSay("@speed fast")!;

        Assert.Equal(GameMasterChat.BadArgumentsLine, outcome.Reply);
        Assert.Null(outcome.Outbox);
    }

    [Theory]
    [InlineData("@speed 3", GameMasterFrames.OrderSuperRun)]
    [InlineData("@immortal on", GameMasterFrames.OrderImmortal)]
    [InlineData("@invisible on", GameMasterFrames.OrderInvisible)]
    [InlineData("@teleportchrtozone 23", GameMasterFrames.OrderTeleportToZone)]
    [InlineData("@teleportchrtozone Friend 23", GameMasterFrames.OrderTeleportToZone)]
    [InlineData("@teleportselftochr Friend", GameMasterFrames.OrderTeleportToChr)]
    [InlineData("@summonmonster 2001 3", GameMasterFrames.OrderSummonMonster)]
    [InlineData("@rmvplayershop Friend", GameMasterFrames.OrderRemovePersonalShop)]
    [InlineData("@rmvmycamp Friend", GameMasterFrames.OrderRemoveMyCamp)]
    public void EachCommandIsRoutedToItsGameServerOrder(string line, ushort order)
    {
        var setup = new Setup();

        var outcome = setup.LoggedInSay(line)!;

        Assert.Null(outcome.Reply);
        Assert.Contains(outcome.Outbox!.Orders, produced => produced.OrderType == order);
    }

    [Fact]
    public void KickDisconnectsAnOnlineCharacterAndKickWithAReasonSendsTheCauseFirst()
    {
        var setup = new Setup();

        var plain = setup.LoggedInSay("@kick Friend")!;
        var withReason = setup.Say("@kick Friend \"spamming\"")!;

        Assert.Contains(Friend, plain.Outbox!.Kicks);
        Assert.Null(plain.Reply);
        Assert.Null(withReason.Reply);
        Assert.NotNull(withReason.Outbox);
        Assert.Empty(withReason.Outbox!.Kicks);
    }

    [Theory]
    [InlineData("@supernotice \"hello\"")]
    [InlineData("@supernotice 2 \"hello\"")]
    [InlineData("@memo Friend \"hello\"")]
    [InlineData("@block Friend 3")]
    [InlineData("@block Friend 3 \"spamming\"")]
    [InlineData("@chatblock Friend 30")]
    [InlineData("@moveblock Friend 30")]
    [InlineData("@tradeblock Friend 30")]
    [InlineData("@blockmycamp Friend 30")]
    [InlineData("@blockmyshop Friend 30")]
    [InlineData("@blockmychat Friend 30")]
    [InlineData("@chatblocklist")]
    [InlineData("@moveblocklist")]
    [InlineData("@tradeblocklist")]
    [InlineData("@commandhelp")]
    [InlineData("@rmvmychat Friend")]
    public void EveryOtherCommandProducesAnAnswer(string line)
    {
        var setup = new Setup();

        var outcome = setup.LoggedInSay(line)!;

        Assert.Null(outcome.Reply);
        Assert.NotNull(outcome.Outbox);
        Assert.Contains(outcome.Audit, entry => entry.Contains(line.Split(' ')[0], StringComparison.Ordinal));
    }
}
