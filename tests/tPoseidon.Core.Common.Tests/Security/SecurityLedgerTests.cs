using tPoseidon.Core.Common.Security;

namespace tPoseidon.Core.Common.Tests.Security;

public sealed class SecurityLedgerTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "tposeidon-ledger-" + Guid.NewGuid().ToString("N"));

    private string LedgerPath => SecurityFiles.LedgerPath(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static AddressRange Range(string text)
    {
        Assert.True(AddressRange.TryParse(text, out var range));
        return range;
    }

    [Fact]
    public void MissingFile_IsAnEmptyLedger()
    {
        var ledger = SecurityLedger.Load(LedgerPath);

        Assert.Empty(ledger.Bans);
        Assert.Empty(ledger.Allow);
    }

    [Fact]
    public void Update_WritesAndReloadsBansAllowsAndPardons()
    {
        SecurityLedger.Update(LedgerPath, ledger =>
        {
            ledger.Ban(Range("203.0.113.9"), Now, Now.AddHours(1), "spam");
            ledger.Ban(Range("198.51.100.0/24"), Now, null, "");
            ledger.AddAllow(Range("10.1.0.0/16"), Now, "office");
        });
        SecurityLedger.Update(LedgerPath, ledger => ledger.Unban(Range("203.0.113.9"), Now.AddMinutes(1)));

        var loaded = SecurityLedger.Load(LedgerPath);

        Assert.Equal("198.51.100.0/24", Assert.Single(loaded.Bans).Address);
        Assert.Equal("10.1.0.0/16", Assert.Single(loaded.Allow).Address);
        var pardon = Assert.Single(loaded.Pardons);
        Assert.Equal("203.0.113.9", pardon.Address);
        Assert.Equal(Now.AddMinutes(1), pardon.At);
    }

    [Fact]
    public void Ban_ReplacesAnEarlierBanOnTheSameRange()
    {
        var ledger = new SecurityLedger();
        ledger.Ban(Range("203.0.113.9"), Now, null, "first");
        ledger.Ban(Range("203.0.113.9"), Now, Now.AddDays(1), "second");

        Assert.Equal("second", Assert.Single(ledger.Bans).Reason);
    }

    [Fact]
    public void Prune_DropsEndedBansAndOldPardons()
    {
        var ledger = new SecurityLedger();
        ledger.Ban(Range("203.0.113.9"), Now, Now.AddMinutes(5), "");
        ledger.Unban(Range("203.0.113.10"), Now);

        ledger.Prune(Now.AddDays(3));

        Assert.Empty(ledger.Bans);
        Assert.Empty(ledger.Pardons);
    }

    [Fact]
    public void Allow_IsNotAddedTwice()
    {
        var ledger = new SecurityLedger();

        Assert.True(ledger.AddAllow(Range("10.0.0.0/8"), Now, ""));
        Assert.False(ledger.AddAllow(Range("10.9.9.9/8"), Now, ""));
        Assert.Equal(1, ledger.RemoveAllow(Range("10.0.0.0/8")));
    }
}
