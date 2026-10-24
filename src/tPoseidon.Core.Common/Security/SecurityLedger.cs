using System.Text.Json;

namespace tPoseidon.Core.Common.Security;

public sealed class SecurityLedger
{
    public static readonly TimeSpan PardonLifetime = TimeSpan.FromDays(2);
    private static readonly TimeSpan LockWait = TimeSpan.FromSeconds(5);

    public List<LedgerBan> Bans { get; set; } = [];

    public List<LedgerAllow> Allow { get; set; } = [];

    public List<LedgerPardon> Pardons { get; set; } = [];

    public static SecurityLedger Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return new SecurityLedger();
        }

        var json = ReadShared(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SecurityLedger();
        }

        var ledger = JsonSerializer.Deserialize<SecurityLedger>(json, SecurityFiles.JsonOptions) ?? new SecurityLedger();
        ledger.Bans ??= [];
        ledger.Allow ??= [];
        ledger.Pardons ??= [];
        return ledger;
    }

    public static SecurityLedger Update(string path, Action<SecurityLedger> change)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(change);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var held = AcquireLock(path + ".lock");
        var ledger = Load(path);
        change(ledger);
        SecurityFiles.WriteAtomic(path, JsonSerializer.Serialize(ledger, SecurityFiles.JsonOptions));
        return ledger;
    }

    public LedgerBan Ban(AddressRange range, DateTimeOffset now, DateTimeOffset? until, string reason)
    {
        ArgumentNullException.ThrowIfNull(range);
        Bans.RemoveAll(ban => string.Equals(ban.Address, range.Text, StringComparison.OrdinalIgnoreCase));
        var entry = new LedgerBan { Address = range.Text, Since = now, Until = until, Reason = reason ?? "" };
        Bans.Add(entry);
        Prune(now);
        return entry;
    }

    public int Unban(AddressRange range, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(range);
        var removed = Bans.RemoveAll(ban => string.Equals(ban.Address, range.Text, StringComparison.OrdinalIgnoreCase));
        Pardons.RemoveAll(pardon => string.Equals(pardon.Address, range.Text, StringComparison.OrdinalIgnoreCase));
        Pardons.Add(new LedgerPardon { Address = range.Text, At = now });
        Prune(now);
        return removed;
    }

    public bool AddAllow(AddressRange range, DateTimeOffset now, string reason)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (Allow.Any(entry => string.Equals(entry.Address, range.Text, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Allow.Add(new LedgerAllow { Address = range.Text, Since = now, Reason = reason ?? "" });
        return true;
    }

    public int RemoveAllow(AddressRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        return Allow.RemoveAll(entry => string.Equals(entry.Address, range.Text, StringComparison.OrdinalIgnoreCase));
    }

    public void Prune(DateTimeOffset now)
    {
        Bans.RemoveAll(ban => ban.Until is { } until && until <= now);
        Pardons.RemoveAll(pardon => now - pardon.At > PardonLifetime);
    }

    private static string ReadShared(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(20);
            }
        }
    }

    private static FileStream AcquireLock(string lockPath)
    {
        var deadline = DateTime.UtcNow + LockWait;
        while (true)
        {
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }
}

public sealed class LedgerBan
{
    public string Address { get; set; } = "";

    public DateTimeOffset Since { get; set; }

    public DateTimeOffset? Until { get; set; }

    public string Reason { get; set; } = "";
}

public sealed class LedgerAllow
{
    public string Address { get; set; } = "";

    public DateTimeOffset Since { get; set; }

    public string Reason { get; set; } = "";
}

public sealed class LedgerPardon
{
    public string Address { get; set; } = "";

    public DateTimeOffset At { get; set; }
}
