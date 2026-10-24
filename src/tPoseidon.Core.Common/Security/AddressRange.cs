using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace tPoseidon.Core.Common.Security;

public sealed class AddressRange
{
    private readonly byte[] _network;
    private readonly int _prefixLength;

    private AddressRange(IPAddress network, int prefixLength)
    {
        _network = network.GetAddressBytes();
        _prefixLength = prefixLength;
        Family = network.AddressFamily;
        Text = prefixLength == _network.Length * 8
            ? network.ToString()
            : string.Create(CultureInfo.InvariantCulture, $"{network}/{prefixLength}");
    }

    public AddressFamily Family { get; }

    public string Text { get; }

    public static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    public static bool TryParse(string? text, out AddressRange range)
    {
        range = null!;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        var slash = trimmed.IndexOf('/', StringComparison.Ordinal);
        var addressText = slash < 0 ? trimmed : trimmed[..slash];
        if (!IPAddress.TryParse(addressText, out var address))
        {
            return false;
        }

        address = Normalize(address);
        var bits = address.GetAddressBytes().Length * 8;
        var prefix = bits;
        if (slash >= 0 && (!int.TryParse(trimmed[(slash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out prefix) || prefix < 0 || prefix > bits))
        {
            return false;
        }

        range = new AddressRange(Masked(address, prefix), prefix);
        return true;
    }

    public bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        address = Normalize(address);
        if (address.AddressFamily != Family)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        var whole = _prefixLength / 8;
        for (var i = 0; i < whole; i++)
        {
            if (bytes[i] != _network[i])
            {
                return false;
            }
        }

        var rest = _prefixLength % 8;
        if (rest == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - rest));
        return (bytes[whole] & mask) == _network[whole];
    }

    public override string ToString() => Text;

    private static IPAddress Masked(IPAddress address, int prefix)
    {
        var bytes = address.GetAddressBytes();
        for (var i = 0; i < bytes.Length; i++)
        {
            var keep = Math.Clamp(prefix - (i * 8), 0, 8);
            bytes[i] &= (byte)(keep == 0 ? 0 : 0xFF << (8 - keep));
        }

        return new IPAddress(bytes);
    }
}
