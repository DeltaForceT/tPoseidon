using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace tPoseidon.Hosting.Servers;

public static class PortProbe
{
    public static bool IsListening(int port)
    {
        if (port is <= 0 or > IPEndPoint.MaxPort)
        {
            return false;
        }

        if (ListenerTable() is { } listeners)
        {
            return listeners.Contains(port);
        }

        using var client = new TcpClient();
        try
        {
            client.Connect(IPAddress.Loopback, port);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static HashSet<int>? ListenerTable()
    {
        try
        {
            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .Select(endpoint => endpoint.Port)
                .ToHashSet();
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException or NetworkInformationException)
        {
            return null;
        }
    }
}
