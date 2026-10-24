using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace tPoseidon.Hosting;

public static class NetworkAddresses
{
    public const string Loopback = "127.0.0.1";

    public static IReadOnlyList<IPAddress> OfThisComputer() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
            .Select(unicast => unicast.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Append(IPAddress.Loopback)
            .Distinct()
            .ToList();

    public static string DefaultLan(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Network.Audience == PlayerAudience.Lan && !string.IsNullOrWhiteSpace(settings.Network.PublicAddress))
        {
            return settings.Network.PublicAddress;
        }

        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up
                && nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(nic => nic.GetIPProperties())
            .Select(properties => (
                HasGateway: properties.GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork && !gateway.Address.Equals(IPAddress.Any)),
                Address: properties.UnicastAddresses
                    .Select(unicast => unicast.Address)
                    .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))))
            .Where(candidate => candidate.Address is not null)
            .OrderByDescending(candidate => candidate.HasGateway)
            .ToList();
        return candidates.Count > 0 ? candidates[0].Address!.ToString() : Loopback;
    }
}
