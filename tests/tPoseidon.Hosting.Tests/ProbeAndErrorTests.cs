using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.Processes;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class ProbeAndErrorTests
{
    [Fact]
    public void ListeningPortIsSeen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            Assert.True(PortProbe.IsListening(port));
        }
        finally
        {
            listener.Stop();
        }

        Assert.False(PortProbe.IsListening(port));
        Assert.False(PortProbe.IsListening(0));
    }

    [Fact]
    public void ListenerOnTheSameAddressIsSeen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            Assert.True(PortProbe.IsListening(port, "127.0.0.1"));
            Assert.True(PortProbe.IsListening(port, "0.0.0.0"));
        }
        finally
        {
            listener.Stop();
        }
    }

    [Theory]
    [InlineData("216.39.108.160", "127.0.0.1", false)]
    [InlineData("127.0.0.1", "127.0.0.1", true)]
    [InlineData("0.0.0.0", "127.0.0.1", true)]
    [InlineData("216.39.108.160", "0.0.0.0", true)]
    [InlineData("::", "127.0.0.1", true)]
    public void OnlyListenersThatShareTheBindAddressClash(string listenerAddress, string bindAddress, bool clash)
    {
        var listeners = new[] { new IPEndPoint(IPAddress.Parse(listenerAddress), 9980) };
        Assert.Equal(clash, PortProbe.Clashes(listeners, 9980, IPAddress.Parse(bindAddress)));
        Assert.False(PortProbe.Clashes(listeners, 9981, IPAddress.Parse(bindAddress)));
    }

    [Fact]
    public void RefusedConnectionExplainsHowToStartPostgres()
    {
        var target = new NpgsqlConnectionStringBuilder { Host = "localhost", Port = 5999, Username = "tposeidon" };
        var error = DatabaseErrors.Explain(new NpgsqlException("refused", new SocketException((int)SocketError.ConnectionRefused)), target);
        Assert.StartsWith("Nothing answered on localhost:5999", error.Summary, StringComparison.Ordinal);
        Assert.Equal(DatabaseErrors.StartHint(), error.Hint);
        Assert.Equal(error.Summary + Environment.NewLine + error.Hint, error.Message);
        Assert.Equal(DatabaseProblem.Unreachable, error.Problem);
    }

    [Fact]
    public void UnknownHostHasNoHint()
    {
        var target = new NpgsqlConnectionStringBuilder { Host = "db.invalid", Port = 5432 };
        var error = DatabaseErrors.Explain(new NpgsqlException("lookup", new SocketException((int)SocketError.HostNotFound)), target);
        Assert.Contains("could not be found", error.Summary, StringComparison.Ordinal);
        Assert.Null(error.Hint);
        Assert.Equal(DatabaseProblem.UnknownHost, error.Problem);
    }

    [Fact]
    public void OldServerNamesTheMinimum()
    {
        var target = new NpgsqlConnectionStringBuilder { Host = "localhost", Port = 5433 };
        var error = DatabaseErrors.TooOld(new Version(12, 4), target, DatabaseAdmin.MinimumMajorVersion, DatabaseAdmin.TestedMajorVersion);
        Assert.Equal(DatabaseProblem.TooOld, error.Problem);
        Assert.StartsWith("PostgreSQL 12.4 on localhost:5433 is too old", error.Summary, StringComparison.Ordinal);
        Assert.Equal(DatabaseErrors.UpgradeHint(), error.Hint);
    }

    [Fact]
    public async Task ProbeOfAClosedPortFailsWithExplanation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var connection = DatabaseAdmin.AdminConnection("127.0.0.1", port, "postgres", "unused");
        var error = await Assert.ThrowsAsync<DatabaseException>(() => DatabaseAdmin.ProbeAsync(connection, CancellationToken.None));
        Assert.Contains($"127.0.0.1:{port}", error.Summary, StringComparison.Ordinal);
        Assert.Equal(DatabaseProblem.Unreachable, error.Problem);
    }

    [Fact]
    public void MeterReadsThisProcess()
    {
        using var self = Process.GetCurrentProcess();
        var meter = new ProcessMeter();
        Assert.NotNull(meter.Sample(self.Id));
        var second = meter.Sample(self.Id);
        Assert.NotNull(second);
        Assert.True(second.Value.MemoryMegabytes > 1);
        Assert.InRange(second.Value.CpuPercent, 0, 100);
    }

    [Fact]
    public void MeterReturnsNothingForAMissingProcess()
    {
        Assert.Null(new ProcessMeter().Sample(int.MaxValue));
    }
}
