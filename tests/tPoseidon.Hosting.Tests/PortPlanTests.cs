using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class PortPlanTests
{
    private static readonly Func<int, bool> NothingInUse = _ => false;

    [Fact]
    public void DistinctFreePortsPass()
    {
        Assert.Null(PortPlan.Validate(new[] { ("Lobby", "9980"), ("Master", " 16646 ") }, NothingInUse));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("80")]
    [InlineData("70000")]
    [InlineData("-5")]
    [InlineData("")]
    public void UnusablePortIsRejected(string text)
    {
        var issue = PortPlan.Validate(new[] { ("Lobby", "9980"), ("Master", text) }, NothingInUse);
        Assert.NotNull(issue);
        Assert.Equal(new[] { "Master" }, issue.Servers);
    }

    [Fact]
    public void DuplicatesNameBothServers()
    {
        var issue = PortPlan.Validate(new[] { ("Lobby", "9980"), ("Master", "16646"), ("Shop", "9980") }, NothingInUse);
        Assert.NotNull(issue);
        Assert.Equal(new[] { "Lobby", "Shop" }, issue.Servers);
        Assert.Contains("same port (9980)", issue.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void BusyPortIsReported()
    {
        var issue = PortPlan.Validate(new[] { ("Lobby", "9980"), ("Master", "16646") }, port => port == 16646);
        Assert.NotNull(issue);
        Assert.Equal(new[] { "Master" }, issue.Servers);
        Assert.Contains("already used", issue.Summary, StringComparison.Ordinal);
    }
}
