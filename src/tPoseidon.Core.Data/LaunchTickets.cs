using System.Net;

namespace tPoseidon.Core.Data;

public interface ILaunchTicketStore
{
    void Issue(uint accountId, IReadOnlyCollection<IPAddress> clientIps, TimeSpan lifetime);

    bool Consume(uint accountId, IPAddress clientIp);
}

public static class LaunchTicket
{
    public const string ClientLaunchPassword = "dummy";

    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    public static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return IPAddress.IsLoopback(address) ? IPAddress.Loopback : address;
    }

    public static bool VerifyLogin(IAccountStore accounts, ILaunchTicketStore tickets, string username, string password, IPAddress clientIp)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(tickets);
        if (password == ClientLaunchPassword
            && accounts.GetAccountByUsername(username) is { } account
            && tickets.Consume(account.AccountId, clientIp))
        {
            return true;
        }

        return accounts.VerifyPassword(username, password);
    }
}

public sealed class InMemoryLaunchTicketStore : ILaunchTicketStore
{
    private readonly TimeProvider _clock;
    private readonly List<(uint AccountId, HashSet<IPAddress> Ips, DateTimeOffset Expires)> _tickets = [];

    public InMemoryLaunchTicketStore(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public void Issue(uint accountId, IReadOnlyCollection<IPAddress> clientIps, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(clientIps);
        lock (_tickets)
        {
            var now = _clock.GetUtcNow();
            _tickets.RemoveAll(ticket => ticket.Expires <= now);
            _tickets.Add((accountId, clientIps.Select(LaunchTicket.Normalize).ToHashSet(), now + lifetime));
        }
    }

    public bool Consume(uint accountId, IPAddress clientIp)
    {
        var ip = LaunchTicket.Normalize(clientIp);
        lock (_tickets)
        {
            var now = _clock.GetUtcNow();
            var index = _tickets.FindIndex(ticket => ticket.AccountId == accountId && ticket.Expires > now && ticket.Ips.Contains(ip));
            if (index < 0)
            {
                return false;
            }

            _tickets.RemoveAt(index);
            return true;
        }
    }
}
