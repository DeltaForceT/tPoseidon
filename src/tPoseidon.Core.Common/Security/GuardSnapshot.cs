using System.Text.Json;

namespace tPoseidon.Core.Common.Security;

public sealed class GuardSnapshot
{
    public string Server { get; set; } = "";

    public int ProcessId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Connections { get; set; }

    public int Pending { get; set; }

    public int TrustedConnections { get; set; }

    public int TrackedAddresses { get; set; }

    public Dictionary<string, long> Counters { get; set; } = new(StringComparer.Ordinal);

    public List<SnapshotBan> Bans { get; set; } = [];

    public List<SnapshotIncident> Incidents { get; set; } = [];

    public bool IsFresh(DateTimeOffset now) => now - UpdatedAt <= SecurityFiles.SnapshotStaleAfter;

    public static GuardSnapshot? TryRead(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return JsonSerializer.Deserialize<GuardSnapshot>(stream, SecurityFiles.JsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(string path) =>
        SecurityFiles.WriteAtomic(path, JsonSerializer.Serialize(this, SecurityFiles.JsonOptions));
}

public sealed class SnapshotIncident
{
    public DateTimeOffset At { get; set; }

    public string Address { get; set; } = "";

    public string Kind { get; set; } = "";

    public string Detail { get; set; } = "";
}
