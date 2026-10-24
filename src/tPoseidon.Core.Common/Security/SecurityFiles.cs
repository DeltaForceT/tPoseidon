using System.Text;
using System.Text.Json;

namespace tPoseidon.Core.Common.Security;

public static class SecurityFiles
{
    public const string LedgerFileName = "bans.json";
    public const string SnapshotExtension = ".guard.json";

    public static readonly TimeSpan SnapshotStaleAfter = TimeSpan.FromSeconds(30);

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string LedgerPath(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        return Path.Combine(directory, LedgerFileName);
    }

    public static string SnapshotPath(string directory, string serverName)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(serverName);
        var safe = new StringBuilder(serverName.Length);
        foreach (var c in serverName)
        {
            safe.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        }

        return Path.Combine(directory, safe + SnapshotExtension);
    }

    public static IReadOnlyList<(string Path, GuardSnapshot Snapshot)> ReadSnapshots(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string Path, GuardSnapshot Snapshot)>();
        foreach (var path in Directory.EnumerateFiles(directory, "*" + SnapshotExtension))
        {
            if (GuardSnapshot.TryRead(path) is { } snapshot)
            {
                found.Add((path, snapshot));
            }
        }

        return found.OrderBy(entry => entry.Snapshot.Server, StringComparer.Ordinal).ToList();
    }

    public static void WriteAtomic(string path, string content)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(content);
        var temporary = path + "." + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".tmp";
        File.WriteAllText(temporary, content);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(temporary, path, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
        }
    }
}
