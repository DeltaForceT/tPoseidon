using System.Net;
using tPoseidon.Core.Common.Security;

namespace tPoseidon.Core.Common.Tests.Security;

public sealed class GuardSettingsTests
{
    [Fact]
    public void Defaults_HaveNoProblems()
    {
        Assert.Empty(new GuardSettings().Problems());
    }

    [Fact]
    public void Json_RoundTripsEverySetting()
    {
        var settings = new GuardSettings { MaxConnectionsPerAddress = 7, TrustedAddresses = ["10.0.0.0/8"], NewConnectionsPerAddressPerSecond = 2.5 };

        var back = GuardSettings.FromJson(settings.ToJson());

        Assert.Equal(settings.ToJson(), back.ToJson());
        Assert.Contains("\"maxConnectionsPerAddress\":7", settings.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Json_KeepsDefaultsForMissingSettings()
    {
        var parsed = GuardSettings.FromJson("{\"maxConnections\":10}");

        Assert.Equal(10, parsed.MaxConnections);
        Assert.Equal(new GuardSettings().MaxConnectionsPerAddress, parsed.MaxConnectionsPerAddress);
    }

    [Fact]
    public void Problems_NameTheBadSettings()
    {
        var settings = new GuardSettings { MaxConnections = 0, TrustedAddresses = ["not an address"] };

        var problems = settings.Problems();

        Assert.Contains(problems, problem => problem.Contains("maxConnections", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("not an address", StringComparison.Ordinal));
    }
}

public sealed class OpcodeAllowanceTests
{
    [Theory]
    [InlineData("0x0018", 0x0018)]
    [InlineData("0X1777", 0x1777)]
    [InlineData("24", 24)]
    public void Opcode_ParsesHexOrDecimal(string text, int expected)
    {
        Assert.True(new OpcodeAllowance { Opcode = text }.TryParseOpcode(out var opcode));

        Assert.Equal(expected, opcode);
    }

    [Fact]
    public void Problems_RejectBadAllowancesAndFrameSizes()
    {
        var settings = new GuardSettings
        {
            MaxFrameBytes = 10,
            OpcodeAllowances = [new OpcodeAllowance { Opcode = "move", Burst = 1, PerSecond = 1 }, new OpcodeAllowance { Opcode = "0x0018", Burst = 0, PerSecond = 1 }],
        };

        var problems = settings.Problems();

        Assert.Contains(problems, problem => problem.Contains("maxFrameBytes", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("'move'", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("burst", StringComparison.Ordinal));
    }

    [Fact]
    public void ReplacedAllowanceList_ComesBackFromJson()
    {
        var parsed = GuardSettings.FromJson("{\"opcodeAllowances\":[{\"opcode\":\"0x0042\",\"name\":\"x\",\"burst\":9,\"perSecond\":3}]}");

        var allowance = Assert.Single(parsed.OpcodeAllowances);
        Assert.Equal(9, allowance.Burst);
    }
}

public sealed class AddressRangeTests
{
    [Theory]
    [InlineData("10.0.0.0/8", "10.200.3.4", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    [InlineData("192.168.1.0/25", "192.168.1.127", true)]
    [InlineData("192.168.1.0/25", "192.168.1.128", false)]
    [InlineData("203.0.113.5", "203.0.113.5", true)]
    [InlineData("203.0.113.5", "203.0.113.6", false)]
    [InlineData("0.0.0.0/0", "8.8.8.8", true)]
    [InlineData("2001:db8::/32", "2001:db8:1::1", true)]
    [InlineData("2001:db8::/32", "10.0.0.1", false)]
    public void Contains_MatchesThePrefix(string range, string address, bool expected)
    {
        Assert.True(AddressRange.TryParse(range, out var parsed));

        Assert.Equal(expected, parsed.Contains(IPAddress.Parse(address)));
    }

    [Fact]
    public void MappedAddresses_MatchTheirIpv4Range()
    {
        Assert.True(AddressRange.TryParse("10.0.0.0/8", out var range));

        Assert.True(range.Contains(IPAddress.Parse("10.1.2.3").MapToIPv6()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("10.0.0.0/33")]
    [InlineData("10.0.0.0/-1")]
    [InlineData("host.example")]
    [InlineData("10.0.0.0/x")]
    public void TryParse_RejectsBadText(string text)
    {
        Assert.False(AddressRange.TryParse(text, out _));
    }

    [Fact]
    public void Text_IsTheNormalisedNetwork()
    {
        Assert.True(AddressRange.TryParse(" 10.9.8.7/16 ", out var range));

        Assert.Equal("10.9.0.0/16", range.Text);
    }
}
