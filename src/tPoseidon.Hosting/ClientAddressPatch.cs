using System.Net;
using System.Net.Sockets;
using System.Text;

namespace tPoseidon.Hosting;

public sealed record ClientAddressResult(string Address, bool Changed, string? BackupPath);

public sealed record ClientLobbySlot(string Service, string PrecedingAddress, string BuiltInAddress);

public static class ClientAddressPatch
{
    public const int SlotBytes = 16;
    public const int ClientLobbyPort = 9980;
    public const string BackupSuffix = ".tposeidon-original";

    public static IReadOnlyList<ClientLobbySlot> Slots { get; } = new[]
    {
        new ClientLobbySlot(ClientFolder.KoreanService, "192.168.12.221", "211.44.251.24"),
        new ClientLobbySlot(ClientFolder.EnglishService, "192.168.12.26", "216.39.108.160"),
    };

    public static int FindSlot(ReadOnlySpan<byte> executable, ClientLobbySlot lobby)
    {
        ArgumentNullException.ThrowIfNull(lobby);
        var marker = Encoding.ASCII.GetBytes(lobby.PrecedingAddress + "\0");
        var first = executable.IndexOf(marker);
        if (first < 0 || executable[(first + marker.Length)..].IndexOf(marker) >= 0 || first % SlotBytes != 0 || first + 2 * SlotBytes > executable.Length)
        {
            return -1;
        }

        var slot = first + SlotBytes;
        var text = executable.Slice(slot, SlotBytes);
        var end = text.IndexOf((byte)0);
        if (end <= 0 || !IsIpv4(Encoding.ASCII.GetString(text[..end])) || text[end..].IndexOfAnyExcept((byte)0) >= 0)
        {
            return -1;
        }

        return slot;
    }

    public static string ReadAddress(ReadOnlySpan<byte> executable, int slot)
    {
        var text = executable.Slice(slot, SlotBytes);
        return Encoding.ASCII.GetString(text[..text.IndexOf((byte)0)]);
    }

    public static void WriteAddress(Span<byte> executable, int slot, string address)
    {
        var bytes = Encoding.ASCII.GetBytes(address);
        if (!IsIpv4(address) || bytes.Length >= SlotBytes)
        {
            throw new ArgumentException($"{address} is not an IPv4 address.", nameof(address));
        }

        var target = executable.Slice(slot, SlotBytes);
        target.Clear();
        bytes.CopyTo(target);
    }

    public static string ClientAddressFor(string serverAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAddress);
        var trimmed = serverAddress.Trim();
        if (IPAddress.TryParse(trimmed, out var parsed))
        {
            return parsed.AddressFamily == AddressFamily.InterNetwork
                ? parsed.ToString()
                : throw new HostingException($"The KR 2014 client only connects to IPv4 addresses, and {trimmed} is not one.", "Use the server's IPv4 address.", null);
        }

        IPAddress[] found;
        try
        {
            found = Dns.GetHostAddresses(trimmed);
        }
        catch (SocketException ex)
        {
            throw new HostingException($"Could not look up {trimmed}: {ex.Message}", "Check the server address, or use its IPv4 address.", ex);
        }

        return found.FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)?.ToString()
            ?? throw new HostingException($"{trimmed} has no IPv4 address, and the KR 2014 client only connects to IPv4 addresses.", "Use the server's IPv4 address.", null);
    }

    public static ClientAddressResult Apply(string executablePath, string serverAddress, int lobbyPort)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (lobbyPort != ClientLobbyPort)
        {
            throw new HostingException(
                $"The KR 2014 client always connects to lobby port {ClientLobbyPort}, but this server's lobby port is {lobbyPort}.",
                $"Set the lobby port back to {ClientLobbyPort} in tposeidon.json (or run tposeidon setup again).",
                null);
        }

        var address = ClientAddressFor(serverAddress);
        var bytes = File.ReadAllBytes(executablePath);
        var slots = Slots.Select(lobby => FindSlot(bytes, lobby)).ToArray();
        if (slots.Any(slot => slot < 0))
        {
            throw new HostingException(
                $"{Path.GetFileName(executablePath)} is not the KR 2014 client tPoseidon knows: the lobby addresses it connects to were not found.",
                "Use the 2014.01.28 KR client, or reinstall it.",
                null);
        }

        if (slots.All(slot => string.Equals(ReadAddress(bytes, slot), address, StringComparison.Ordinal)))
        {
            return new ClientAddressResult(address, false, null);
        }

        string? backup = null;
        var backupPath = executablePath + BackupSuffix;
        try
        {
            if (!File.Exists(backupPath))
            {
                File.Copy(executablePath, backupPath);
                backup = backupPath;
            }

            foreach (var slot in slots)
            {
                WriteAddress(bytes, slot, address);
            }

            File.WriteAllBytes(executablePath, bytes);
        }
        catch (IOException ex)
        {
            throw new HostingException($"Could not update {executablePath}: {ex.Message}", "Close the game client, then try again.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new HostingException($"Could not update {executablePath}: {ex.Message}", "Move the client to a folder you can write to (not Program Files), or give your account write access to it.", ex);
        }

        return new ClientAddressResult(address, true, backup);
    }

    private static bool IsIpv4(string text) =>
        IPAddress.TryParse(text, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork && parsed.ToString() == text;
}
