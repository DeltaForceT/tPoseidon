using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.WebShop;

namespace tPoseidon.WebShop.Tests;

public sealed class ShopSessionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly IPAddress Player = IPAddress.Parse("192.168.1.20");

    [Fact]
    public void OpenedSessionsAreFoundFromTheSameAddressAndRenewed()
    {
        var sessions = new ShopSessions();
        var opened = sessions.Open(7, "probe", 1, Player, Now);

        var found = sessions.Find(opened.Token, Player, Now.AddMinutes(20));

        Assert.NotNull(found);
        Assert.Equal(7u, found.AccountId);
        Assert.Equal(Now.AddMinutes(20) + ShopSessions.Lifetime, found.ExpiresAt);
        Assert.NotNull(sessions.Find(opened.Token, Player, Now.AddMinutes(45)));
    }

    [Fact]
    public void TokensAreRandomAndTheFormTokenDiffersFromTheCookie()
    {
        var sessions = new ShopSessions();
        var first = sessions.Open(7, "probe", 1, Player, Now);
        var second = sessions.Open(7, "probe", 1, Player, Now);

        Assert.NotEqual(first.Token, second.Token);
        Assert.NotEqual(first.Token, first.FormToken);
        Assert.Equal(ShopSessions.TokenBytes * 2, first.Token.Length);
    }

    [Fact]
    public void AnotherAddressOrAnExpiredSessionIsRefusedAndDropped()
    {
        var sessions = new ShopSessions();
        var stolen = sessions.Open(7, "probe", 1, Player, Now);
        var stale = sessions.Open(8, "other", 1, Player, Now);

        Assert.Null(sessions.Find(stolen.Token, IPAddress.Parse("10.0.0.9"), Now));
        Assert.Null(sessions.Find(stolen.Token, Player, Now));
        Assert.Null(sessions.Find(stale.Token, Player, Now + ShopSessions.Lifetime));
        Assert.Null(sessions.Find("unknown", Player, Now));
        Assert.Null(sessions.Find(null, Player, Now));
    }

    [Fact]
    public void ClosingAndPruningRemoveSessions()
    {
        var sessions = new ShopSessions();
        var closed = sessions.Open(7, "probe", 1, Player, Now);
        sessions.Open(8, "other", 1, Player, Now);
        sessions.Close(closed.Token);

        Assert.Null(sessions.Find(closed.Token, Player, Now));
        sessions.Prune(Now + ShopSessions.Lifetime);
        Assert.Equal(0, sessions.Count);
    }

    [Fact]
    public void OnlyAnInGameAccountFromItsLoginAddressPasses()
    {
        var inGame = new ShopAccount(7, "probe", ShopAccountRules.InGameState, Player);

        Assert.Equal(ShopRefusal.None, ShopGate.Check(inGame, Player));
        Assert.Equal(ShopRefusal.UnknownAccount, ShopGate.Check(null, Player));
        Assert.Equal(ShopRefusal.NotInGame, ShopGate.Check(inGame with { GameState = 3 }, Player));
        Assert.Equal(ShopRefusal.OtherAddress, ShopGate.Check(inGame, IPAddress.Parse("10.0.0.9")));
        Assert.Equal(ShopRefusal.OtherAddress, ShopGate.Check(inGame with { LatestIp = null }, Player));
    }

    [Fact]
    public void LoopbackAndMappedAddressesCompareEqual()
    {
        var local = new ShopAccount(7, "probe", ShopAccountRules.InGameState, IPAddress.Loopback);

        Assert.Equal(ShopRefusal.None, ShopGate.Check(local, IPAddress.IPv6Loopback));
        Assert.Equal(ShopRefusal.None, ShopGate.Check(local with { LatestIp = Player }, Player.MapToIPv6()));
    }
}
