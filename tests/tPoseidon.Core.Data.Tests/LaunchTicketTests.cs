using System.Net;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data.Tests;

public sealed class LaunchTicketTests
{
    private static readonly IPAddress Local = IPAddress.Loopback;
    private static readonly IPAddress Remote = IPAddress.Parse("203.0.113.9");

    private sealed class SteppingClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan span) => _now += span;
    }

    private static (InMemoryAccountStore Accounts, uint Id) AccountWithPassword(string username, string password)
    {
        var accounts = new InMemoryAccountStore();
        var account = accounts.RegisterAccount(username, PasswordHasher.Hash(password));
        return (accounts, account.AccountId);
    }

    [Fact]
    public void ATicketLogsInOnceWithTheClientLaunchPassword()
    {
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore();
        tickets.Issue(id, [Local], LaunchTicket.Lifetime);

        Assert.True(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
    }

    [Fact]
    public void NoTicketMeansRefusal()
    {
        var (accounts, _) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore();

        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
    }

    [Fact]
    public void AnExpiredTicketIsRefused()
    {
        var clock = new SteppingClock();
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore(clock);
        tickets.Issue(id, [Local], LaunchTicket.Lifetime);
        clock.Advance(LaunchTicket.Lifetime + TimeSpan.FromSeconds(1));

        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
    }

    [Fact]
    public void ATicketOnlyWorksFromItsOwnAddress()
    {
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore();
        tickets.Issue(id, [Remote], LaunchTicket.Lifetime);

        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
        Assert.True(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Remote));
    }

    [Fact]
    public void ATicketBelongsToOneAccount()
    {
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        accounts.RegisterAccount("other", PasswordHasher.Hash("other-secret"));
        var tickets = new InMemoryLaunchTicketStore();
        tickets.Issue(id, [Local], LaunchTicket.Lifetime);

        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "other", LaunchTicket.ClientLaunchPassword, Local));
    }

    [Fact]
    public void LoopbackSpellingsMatch()
    {
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore();
        tickets.Issue(id, [IPAddress.IPv6Loopback], LaunchTicket.Lifetime);

        Assert.True(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, IPAddress.Loopback.MapToIPv6()));
    }

    [Fact]
    public void NormalPasswordLoginsAreUnchanged()
    {
        var (accounts, id) = AccountWithPassword("player", "real-secret");
        var tickets = new InMemoryLaunchTicketStore();
        tickets.Issue(id, [Local], LaunchTicket.Lifetime);

        Assert.True(LaunchTicket.VerifyLogin(accounts, tickets, "player", "real-secret", Remote));
        Assert.False(LaunchTicket.VerifyLogin(accounts, tickets, "player", "wrong", Local));
        Assert.True(LaunchTicket.VerifyLogin(accounts, tickets, "player", LaunchTicket.ClientLaunchPassword, Local));
    }

    [Fact]
    public void AnAccountWhosePasswordIsTheLaunchPasswordStillLogsInNormally()
    {
        var (accounts, _) = AccountWithPassword("player", LaunchTicket.ClientLaunchPassword);

        Assert.True(LaunchTicket.VerifyLogin(accounts, new InMemoryLaunchTicketStore(), "player", LaunchTicket.ClientLaunchPassword, Remote));
    }
}
