using System.Security.Cryptography;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public static class RosemaryExchangeRows
{
    public const string ListTable = "Exchange_Shop_Square";
    private const int FingerprintLength = 8;

    private static readonly int[] OfferIds =
    [
        148, 576,
        473, 474, 475, 476, 477, 478,
        597, 598, 599, 600, 1001, 1002,
        1654, 1655, 1656, 1657, 1658, 1659,
        1670, 2004, 2017,
        1871, 1872, 1873, 1874, 1875, 1876, 1877, 1878, 1879, 1880, 1881, 1882,
        2020, 2021, 2022,
        2028, 2029, 2030,
        2055, 2056, 2057,
        2070, 2071,
        2076, 2077, 2078,
        2089, 2090, 2091,
        2092, 2093, 2094, 2095, 2096, 2097, 2098,
        2119, 2120, 2121, 2122,
        2236,
    ];

    public static IReadOnlyList<int> Offers => OfferIds;

    public static LibConfigRowPatch Patch { get; } = new(
        ListTable,
        "ExchangeID",
        ["ExchangeID"],
        OfferIds.Select(id => (IReadOnlyList<string>)[id.ToString(System.Globalization.CultureInfo.InvariantCulture)]).ToList());

    public static string Fingerprint { get; } = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(string.Join(',', OfferIds))))[..FingerprintLength].ToLowerInvariant();

    public static byte[] Merge(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return LibConfigXmlRowEditor.AddMissingRows(xml, Patch).Xml;
    }
}
