using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace tPoseidon.Core.Common.Updates;

public static class ReleaseAssets
{
    public const string ChecksumFile = "SHA256SUMS";
    public const string SignatureFile = "SHA256SUMS.sig";
    public const string KeyFileName = "release-key.json";

    public static string PackageName(string version, string rid) =>
        $"tposeidon-{version}-{rid}{(rid.StartsWith("win-", StringComparison.Ordinal) ? ".zip" : ".tar.gz")}";
}

public sealed record ChecksumEntry(string Sha256, string FileName);

public static class ReleaseChecksums
{
    private const int HashLength = 64;
    private const int SeparatorLength = 2;

    public static byte[] Build(IEnumerable<ChecksumEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var text = new StringBuilder();
        foreach (var entry in entries.OrderBy(each => each.FileName, StringComparer.Ordinal))
        {
            text.Append(entry.Sha256.ToLowerInvariant()).Append("  ").Append(entry.FileName).Append('\n');
        }

        return new UTF8Encoding(false).GetBytes(text.ToString());
    }

    public static string HashOf(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static IReadOnlyList<ChecksumEntry> Parse(ReadOnlySpan<byte> content)
    {
        var entries = new List<ChecksumEntry>();
        foreach (var raw in Encoding.UTF8.GetString(content).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length <= HashLength + SeparatorLength || !IsHex(line.AsSpan(0, HashLength)) || line[HashLength] != ' ' || line[HashLength + 1] is not (' ' or '*'))
            {
                throw new InvalidDataException("The checksum list has a line that is not '<sha256>  <file>'.");
            }

            entries.Add(new ChecksumEntry(line[..HashLength].ToLower(CultureInfo.InvariantCulture), line[(HashLength + SeparatorLength)..]));
        }

        return entries;
    }

    private static bool IsHex(ReadOnlySpan<char> text)
    {
        foreach (var character in text)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
