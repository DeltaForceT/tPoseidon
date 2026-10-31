using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace tPoseidon.Hosting.Servers;

public static class PortProbe
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(1);

    public static bool IsListening(int port) => IsListening(port, IPAddress.Any);

    public static bool IsListening(int port, string bindAddress) =>
        IsListening(port, IPAddress.TryParse(bindAddress, out var address) ? address : IPAddress.Any);

    public static bool IsListening(int port, IPAddress bindAddress)
    {
        ArgumentNullException.ThrowIfNull(bindAddress);
        if (port is <= 0 or > IPEndPoint.MaxPort)
        {
            return false;
        }

        if (ListenerTable() is { } listeners && Clashes(listeners, port, bindAddress))
        {
            return true;
        }

        return Accepts(port, bindAddress);
    }

    public static bool Accepts(int port, IPAddress bindAddress)
    {
        using var client = new TcpClient();
        try
        {
            var target = IsWildcard(bindAddress) ? IPAddress.Loopback : bindAddress;
            return client.ConnectAsync(target, port).Wait(ConnectTimeout) && client.Connected;
        }
        catch (Exception ex) when (ex is SocketException or AggregateException or ObjectDisposedException)
        {
            return false;
        }
    }

    public static bool Clashes(IEnumerable<IPEndPoint> listeners, int port, IPAddress bindAddress)
    {
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(bindAddress);
        return listeners.Any(listener => listener.Port == port
            && (IsWildcard(bindAddress) || IsWildcard(listener.Address) || listener.Address.Equals(bindAddress)));
    }

    private static bool IsWildcard(IPAddress address) =>
        address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);

    private static IPEndPoint[]? ListenerTable()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NetworkInformationException)
        {
            return null;
        }
    }
}
