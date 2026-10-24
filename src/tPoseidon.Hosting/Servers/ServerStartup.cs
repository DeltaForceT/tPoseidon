using tPoseidon.Core.Common;
using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting.Servers;

public interface IStartupObserver : IProgress<ServerStartReport>
{
    void CheckingDatabase(string database);

    void UpdatingTables();

    void StartingServers();

    void RollingBack();
}

public static class ServerStartup
{
    public static readonly TimeSpan DatabaseReadyTimeout = TimeSpan.FromSeconds(15);

    public static string CurrentVersion { get; } = BuildIdentity.Current.Version;

    public static bool NeedsBackupBeforeStart(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return !string.IsNullOrEmpty(settings.LastStartedVersion)
            && !string.Equals(settings.LastStartedVersion, CurrentVersion, StringComparison.Ordinal);
    }

    public static async Task PrepareDatabaseAsync(InstallPaths paths, HostSettings settings, IStartupObserver observer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(observer);
        ClientFolder.EnsureUsable(settings);
        observer.CheckingDatabase(settings.Database.Describe());
        await DatabaseAdmin.WaitUntilReadyAsync(settings.Database.Connection, DatabaseReadyTimeout, ct).ConfigureAwait(false);
        observer.UpdatingTables();
        await DatabaseAdmin.ApplyMigrationsAsync(paths, settings.Database, null, ct).ConfigureAwait(false);
    }

    public static async Task StartAllAsync(InstallPaths paths, HostSettings settings, IStartupObserver observer, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(observer);
        await PrepareDatabaseAsync(paths, settings, observer, ct).ConfigureAwait(false);

        observer.StartingServers();
        var supervisor = new ServerSupervisor(paths, settings);
        var started = new List<ServerDefinition>();
        try
        {
            foreach (var server in ServerCatalog.Enabled(settings))
            {
                await supervisor.StartAsync(server, observer, ct).ConfigureAwait(false);
                started.Add(server);
            }
        }
        catch (Exception ex) when (ex is HostingException or OperationCanceledException)
        {
            observer.RollingBack();
            foreach (var server in Enumerable.Reverse(started))
            {
                supervisor.Stop(server);
            }

            throw;
        }

        settings.LastStartedVersion = CurrentVersion;
        settings.Save(paths);
    }

    public static IReadOnlyList<ServerDefinition> StopAll(InstallPaths paths, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        var supervisor = new ServerSupervisor(paths, settings);
        var stopped = new List<ServerDefinition>();
        foreach (var server in ServerCatalog.All.Reverse())
        {
            if (supervisor.Stop(server))
            {
                stopped.Add(server);
            }
        }

        return stopped;
    }
}
