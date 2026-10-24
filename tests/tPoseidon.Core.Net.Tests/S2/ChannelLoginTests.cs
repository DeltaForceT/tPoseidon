using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using KrChannelIntroPacket = tPoseidon.Kr2014.Wire.ChannelIntroPacket;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class ChannelLoginTests
{
    private const long Authcode = 0x024E_D19C_ED25_F468;

    private static (InMemoryAccountStore Store, Account Account) StoreWithAccount()
    {
        var store = new InMemoryAccountStore();
        var account = new Account
        {
            AccountId = 0x061E8BC1,
            Username = "player",
            PasswordPlaceholder = string.Empty,
            CharacterIds = Array.Empty<ulong>(),
        };
        store.SeedAccount(account, Array.Empty<Character>());
        return (store, account);
    }

    private static byte[] TokenEchoedFrom(LoginResultPacket loginResult) =>
        loginResult.EncodePayload()[..ChannelLoginToken.Bytes];

    [Fact]
    public void TheCapturedLoginResultAndChannelIntroCarryTheSameTwelveBytes()
    {
        Assert.Equal(
            TokenEchoedFrom(LoginResultPacket.SnapshotDefault),
            tPoseidon.Core.Net.Packets.ChannelIntroPacket.SnapshotDefault.LoginResultToken);
    }

    [Fact]
    public void TheTokenIsTheAccountCodeThenTheAuthcode()
    {
        var token = ChannelLoginToken.Read(LoginResultPacket.Create(0x061E8BC1, Authcode, "World", "Main").EncodePayload().AsSpan(0, ChannelLoginToken.Bytes));

        Assert.Equal(0x061E8BC1u, token.AccountId);
        Assert.Equal(Authcode, token.Authcode);
    }

    [Fact]
    public void ATokenOfTheWrongLengthIsABadPacket()
    {
        Assert.Throws<tPoseidon.Core.Common.InvalidPacketException>(() => ChannelLoginToken.Read(new byte[11]));
    }

    [Fact]
    public void LobbyLoginThenServerSelectLetsTheChannelIn()
    {
        var (store, account) = StoreWithAccount();
        store.IssueGameAuthcode(account.AccountId, Authcode, IPAddress.Loopback);
        Assert.True(store.TransitionToServerSelected(account.AccountId, 0, 0));

        var token = ChannelLoginToken.Read(TokenEchoedFrom(LoginResultPacket.Create(account.AccountId, Authcode, "World", "Main")));

        Assert.Equal(ChannelLoginRefusal.None, ChannelLogin.Check(token, store.GetGameLogin(token.AccountId)));
    }

    [Fact]
    public void AWrongAuthcodeIsRefusedWith60101()
    {
        var (store, account) = StoreWithAccount();
        store.IssueGameAuthcode(account.AccountId, Authcode, IPAddress.Loopback);
        store.TransitionToServerSelected(account.AccountId, 0, 0);

        var refusal = ChannelLogin.Check(new ChannelLoginToken(account.AccountId, Authcode + 1), store.GetGameLogin(account.AccountId));

        Assert.Equal(ChannelLoginRefusal.InvalidAuthcode, refusal);
        Assert.Equal(60101u, (uint)refusal);
    }

    [Fact]
    public void AnUnknownAccountIsRefusedWith60100()
    {
        var (store, _) = StoreWithAccount();

        Assert.Equal(ChannelLoginRefusal.AccountNotFound, ChannelLogin.Check(new ChannelLoginToken(77, Authcode), store.GetGameLogin(77)));
    }

    [Fact]
    public void AnAccountThatDidNotSelectAServerIsRefusedWith60102()
    {
        var (store, account) = StoreWithAccount();
        store.IssueGameAuthcode(account.AccountId, Authcode, IPAddress.Loopback);

        Assert.Equal(ChannelLoginRefusal.InvalidAccountState, ChannelLogin.Check(new ChannelLoginToken(account.AccountId, Authcode), store.GetGameLogin(account.AccountId)));
    }

    [Fact]
    public void AnAccountThatNeverLoggedInThroughTheLobbyIsRefused()
    {
        var (store, account) = StoreWithAccount();

        Assert.NotEqual(ChannelLoginRefusal.None, ChannelLogin.Check(new ChannelLoginToken(account.AccountId, 0), store.GetGameLogin(account.AccountId)));
    }

    [Fact]
    public void ServerSelectNeedsTheLoggedInState()
    {
        var (store, account) = StoreWithAccount();

        Assert.False(store.TransitionToServerSelected(account.AccountId, 0, 0));
        store.IssueGameAuthcode(account.AccountId, Authcode, IPAddress.Loopback);
        Assert.True(store.TransitionToServerSelected(account.AccountId, 0, 0));
        Assert.False(store.TransitionToServerSelected(account.AccountId, 0, 0));
    }

    [Fact]
    public void ARefusalIsTheCodeAsALittleEndianU32()
    {
        Assert.Equal(new byte[] { 0xC5, 0xEA, 0x00, 0x00 }, ChannelLogin.RefusalPayload(ChannelLoginRefusal.InvalidAuthcode));
        Assert.Equal(new byte[] { 0xF3, 0x03, 0x00, 0x00 }, ChannelLogin.RefusalPayload(ChannelLoginRefusal.DuplicateUser));
        Assert.Equal(0x07D1, ChannelLogin.RefusalOpcode);
    }

    [Fact]
    public void AKr2014ChannelIntroCarriesTheTokenAfterTheVersion()
    {
        var token = TokenEchoedFrom(LoginResultPacket.Create(0x061E8BC1, Authcode, "World", "Main"));
        var intro = new KrChannelIntroPacket
        {
            Username = "player",
            Password = "secret",
            Version = ClientVersion.Kr2014,
            LoginResultToken = token,
        };

        var decoded = KrChannelIntroPacket.DecodePayload(intro.EncodePayload());

        Assert.Equal(new ChannelLoginToken(0x061E8BC1, Authcode), ChannelLoginToken.Read(decoded.LoginResultToken));
    }

    [Fact]
    public void TheKr2014ClientSendsVersion4121Point4161()
    {
        Assert.Equal(new ClientVersion(0x1019, 0x1041), ClientVersion.Kr2014);
        Assert.Equal(new byte[] { 0xCF, 0xB2, 0x97, 0xB2 }, ClientVersion.Kr2014.ToWire());
    }

    [Fact]
    public void TheVersionIsCheckedLikeTheKrLoginServer()
    {
        Assert.Equal(ChannelLoginRefusal.None, ChannelLogin.CheckVersion(ClientVersion.Kr2014, ClientVersion.Kr2014));
        Assert.Equal(ChannelLoginRefusal.VersionMissing, ChannelLogin.CheckVersion(new ClientVersion(0, 0), ClientVersion.Kr2014));
        Assert.Equal(ChannelLoginRefusal.VersionMismatch, ChannelLogin.CheckVersion(new ClientVersion(0x1019, 0x1040), ClientVersion.Kr2014));
        Assert.Equal(0x3F1u, (uint)ChannelLoginRefusal.VersionMissing);
        Assert.Equal(0x3F2u, (uint)ChannelLoginRefusal.VersionMismatch);
    }

    [Fact]
    public void TheCapturedKr2014IntroCarriesTheClientVersion()
    {
        var captured = new byte[]
        {
            0x64, 0x65, 0x76, 0x00,
            0x64, 0x65, 0x73, 0x63, 0x00,
            0xCF, 0xB2, 0x97, 0xB2,
            0xC1, 0x8B, 0x1E, 0x06, 0x68, 0xF4, 0x25, 0xED,
            0x9C, 0xD1, 0x4E, 0x02,
            0x00,
        };

        var intro = KrChannelIntroPacket.DecodePayload(captured);

        Assert.Equal(ClientVersion.Kr2014, intro.Version);
        Assert.Equal(captured, intro.EncodePayload());
    }

    [Theory]
    [InlineData("4121.4161", true, 4121, 4161)]
    [InlineData(" 7 . 125 ", true, 7, 125)]
    [InlineData("4121", false, 0, 0)]
    [InlineData(null, false, 0, 0)]
    public void TheIniVersionIsMajorDotMinor(string? text, bool ok, int major, int minor)
    {
        Assert.Equal(ok, ClientVersion.TryParse(text, out var version));
        Assert.Equal(new ClientVersion((ushort)major, (ushort)minor), version);
    }
}
