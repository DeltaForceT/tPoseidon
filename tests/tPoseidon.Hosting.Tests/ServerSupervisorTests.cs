using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class ServerSupervisorTests
{
    private static ServerDefinition Lobby => ServerCatalog.All.Single(server => server.Name == "Kr-Lobby");

    [Fact]
    public void NoPidFileMeansStopped()
    {
        using var temp = new TempFolder();
        var supervisor = new ServerSupervisor(new InstallPaths(temp.Path), new HostSettings());
        var state = supervisor.State(Lobby);
        Assert.False(state.Running);
        Assert.Null(state.StartedUtc);
        Assert.False(supervisor.AnyRunning());
        Assert.False(supervisor.Stop(Lobby));
    }

    [Fact]
    public void GarbledPidFileIsRemoved()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        temp.WriteFile(Path.Combine("run", "Kr-Lobby.pid"), "not a pid");
        Assert.False(new ServerSupervisor(paths, new HostSettings()).State(Lobby).Running);
        Assert.False(File.Exists(paths.PidFile("Kr-Lobby")));
    }

    [Fact]
    public void PidFileOfAnotherProcessIsNotTrusted()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        using var self = Process.GetCurrentProcess();
        var wrongStart = self.StartTime.ToUniversalTime().AddHours(-3).Ticks;
        temp.WriteFile(Path.Combine("run", "Kr-Lobby.pid"), string.Create(CultureInfo.InvariantCulture, $"{self.Id}\n{wrongStart}\n"));
        Assert.False(new ServerSupervisor(paths, new HostSettings()).State(Lobby).Running);
        Assert.False(File.Exists(paths.PidFile("Kr-Lobby")));
    }

    [Fact]
    public void MatchingPidFileReportsTheProcess()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        using var self = Process.GetCurrentProcess();
        var start = self.StartTime.ToUniversalTime();
        temp.WriteFile(Path.Combine("run", "Kr-Lobby.pid"), string.Create(CultureInfo.InvariantCulture, $"{self.Id}\n{start.Ticks}\n"));
        var state = new ServerSupervisor(paths, new HostSettings()).State(Lobby);
        Assert.True(state.Running);
        Assert.Equal(self.Id, state.ProcessId);
        Assert.Equal(start, state.StartedUtc);
    }

    [Fact]
    public async Task MissingExecutableIsExplained()
    {
        using var temp = new TempFolder();
        var settings = new HostSettings { Ports = { Lobby = FreePort() } };
        var supervisor = new ServerSupervisor(new InstallPaths(temp.Path), settings);
        var error = await Assert.ThrowsAsync<HostingException>(() => supervisor.StartAsync(Lobby, null, CancellationToken.None));
        Assert.Contains("is missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogTailSaysWhenThereIsNoLog()
    {
        using var temp = new TempFolder();
        var tail = new ServerSupervisor(new InstallPaths(temp.Path), new HostSettings()).LogTail(Lobby);
        Assert.StartsWith("No log was written", tail, StringComparison.Ordinal);
    }

    [Fact]
    public void BackupIsOfferedOnlyAfterAVersionChange()
    {
        Assert.False(ServerStartup.NeedsBackupBeforeStart(new HostSettings()));
        Assert.False(ServerStartup.NeedsBackupBeforeStart(new HostSettings { LastStartedVersion = ServerStartup.CurrentVersion }));
        Assert.True(ServerStartup.NeedsBackupBeforeStart(new HostSettings { LastStartedVersion = "0.0.1" }));
    }

    [Fact]
    public void StopAllWithNothingRunningStopsNothing()
    {
        using var temp = new TempFolder();
        Assert.Empty(ServerStartup.StopAll(new InstallPaths(temp.Path), new HostSettings()));
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
