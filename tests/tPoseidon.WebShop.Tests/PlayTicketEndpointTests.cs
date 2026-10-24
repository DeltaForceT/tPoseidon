using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using tPoseidon.Core.Common;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.WebShop.Tests;

public sealed class PlayTicketEndpointTests
{
    private static readonly IPAddress Caller = IPAddress.Parse("203.0.113.9");

    private static (PlayTicketEndpoint Endpoint, InMemoryLaunchTicketStore Tickets, InMemoryAccountStore Accounts) Build(GuardSettings? settings = null)
    {
        var accounts = new InMemoryAccountStore();
        accounts.RegisterAccount("player", PasswordHasher.Hash("real-secret"));
        var tickets = new InMemoryLaunchTicketStore();
        var throttle = new LoginThrottle(settings ?? new GuardSettings { LoginFailuresBeforeDelay = 100 }, TimeProvider.System);
        return (new PlayTicketEndpoint(accounts, tickets, throttle), tickets, accounts);
    }

    private static async Task<int> PostAsync(PlayTicketEndpoint endpoint, string username, string password)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = Caller;
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes($"username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}"));
        await endpoint.HandleAsync(context);
        return context.Response.StatusCode;
    }

    [Fact]
    public async Task ARightPasswordIssuesATicketBoundToTheCaller()
    {
        var (endpoint, tickets, accounts) = Build();
        var id = accounts.GetAccountByUsername("player")!.AccountId;

        Assert.Equal(StatusCodes.Status200OK, await PostAsync(endpoint, "player", "real-secret"));

        Assert.False(tickets.Consume(id, IPAddress.Loopback));
        Assert.True(tickets.Consume(id, Caller));
        Assert.False(tickets.Consume(id, Caller));
    }

    [Fact]
    public async Task AWrongPasswordIsRefusedWithoutATicket()
    {
        var (endpoint, tickets, accounts) = Build();

        Assert.Equal(StatusCodes.Status401Unauthorized, await PostAsync(endpoint, "player", "wrong"));
        Assert.Equal(StatusCodes.Status401Unauthorized, await PostAsync(endpoint, "nobody", "real-secret"));
        Assert.False(tickets.Consume(accounts.GetAccountByUsername("player")!.AccountId, Caller));
    }

    [Fact]
    public async Task RepeatedFailuresLockTheAccountOut()
    {
        var (endpoint, tickets, accounts) = Build(new GuardSettings { LoginFailuresBeforeDelay = 100, AccountLockoutFailures = 3 });

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, await PostAsync(endpoint, "player", "wrong"));
        }

        Assert.Equal(StatusCodes.Status429TooManyRequests, await PostAsync(endpoint, "player", "real-secret"));
        Assert.False(tickets.Consume(accounts.GetAccountByUsername("player")!.AccountId, Caller));
    }

    [Fact]
    public async Task AMissingFieldIsABadRequest()
    {
        var (endpoint, _, _) = Build();

        Assert.Equal(StatusCodes.Status400BadRequest, await PostAsync(endpoint, "player", ""));
    }
}
