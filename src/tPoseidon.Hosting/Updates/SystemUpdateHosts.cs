using System.Diagnostics;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.Database;
using tPoseidon.Hosting.GameMaster;
using tPoseidon.Hosting.Processes;
using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Updates;

public sealed class SystemUpdateHost : IUpdateHost
{
    private readonly InstallPaths _paths;
    private readonly HostSettings _settings;

    public SystemUpdateHost(InstallPaths paths, HostSettings settings)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public bool ServersRunning() => new ServerSupervisor(_paths, _settings).AnyRunning();

    public async Task SendNoticeAsync(string text, CancellationToken ct)
    {
        if (!_settings.Updates.CanSendNotice)
        {
            return;
        }

        var command = GameMasterToolCommands.Find("super-notice")
            ?? throw new HostingException("This build has no game master notice command.");
        var call = GmTool.Prepare(command, ["0", text]);
        var login = new GmLogin(_settings.Updates.NoticeCharacter, _settings.Updates.NoticePassword);
        var outcome = await GmTool.RunAsync(GmToolTransport.Shared, GmToolEndpoint.For(_settings, _paths), login, call, ct).ConfigureAwait(false);
        if (!outcome.Ok || outcome.Refused)
        {
            throw new HostingException("The game master login for the in-game notice was refused.");
        }
    }

    public Task StopServersAsync(CancellationToken ct) =>
        Task.Run(() => ServerStartup.StopAll(_paths, _settings), ct);

    public Task StartServersAsync(CancellationToken ct) =>
        ServerStartup.StartAllAsync(_paths, _settings, new QuietObserver(), ct);

    public Task<string> BackupAsync(CancellationToken ct) =>
        Task.Run(() => DatabaseBackup.Run(_paths, _settings, DatabaseBackup.Locate(_settings)), ct);

    public void StartHelper(int processId) =>
        UpdateHelper.Start(_paths, processId);

    private sealed class QuietObserver : IStartupObserver
    {
        public void CheckingDatabase(string database)
        {
        }

        public void UpdatingTables()
        {
        }

        public void RebuildingServerData()
        {
        }

        public void StartingServers()
        {
        }

        public void RollingBack()
        {
        }

        public void Report(ServerStartReport value)
        {
        }
    }
}

public static class UpdateHelper
{
    public const string FolderPrefix = "tposeidon-update-";

    public static void Start(InstallPaths paths, int processId)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var helperFolder = Path.Combine(Path.GetTempPath(), FolderPrefix + Guid.NewGuid().ToString("N"));
        CopyFolder(paths.App, helperFolder);
        var program = Path.Combine(helperFolder, InstallPaths.Executable("tposeidon"));
        var info = new ProcessStartInfo(program) { UseShellExecute = false, WorkingDirectory = helperFolder, CreateNoWindow = true };
        foreach (var argument in Arguments(paths, processId))
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            Process.Start(info)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new HostingException($"The update program could not be started: {ex.Message}", ex);
        }
    }

    public static IReadOnlyList<string> Arguments(InstallPaths paths, int processId)
    {
        return ["update", "apply", "--home", paths.Root, "--pid", processId.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    }

    public static int RemoveFinished()
    {
        var remaining = 0;
        foreach (var folder in Directory.EnumerateDirectories(Path.GetTempPath(), FolderPrefix + "*"))
        {
            try
            {
                UpdateWork.DeleteFolder(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                remaining++;
            }
        }

        return remaining;
    }

    private static void CopyFolder(string source, string target)
    {
        if (!Directory.Exists(source))
        {
            throw new HostingException($"The program folder {source} does not exist.");
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
        {
            var destination = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}

public sealed class ProcessApplyHost : IApplyHost
{
    public static readonly TimeSpan SettleTime = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _settle;

    public ProcessApplyHost(TimeSpan? settle = null)
    {
        _settle = settle ?? SettleTime;
    }

    public bool WaitForExit(int processId, TimeSpan timeout)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit(timeout);
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    public async Task<HealthReport> StartAndCheckAsync(InstallPaths paths, string expectedVersion, bool startServers, CancellationToken ct)
    {
        var program = paths.AppExecutable("tposeidon");
        var version = await Task.Run(() => ToolRunner.Capture(program, ["version"]), ct).ConfigureAwait(false);
        if (version.ExitCode != 0 || !version.Output.Contains(expectedVersion, StringComparison.Ordinal))
        {
            return new HealthReport(false, $"The new program did not report version {expectedVersion}: {Tail(version.Output)}");
        }

        if (!startServers)
        {
            return new HealthReport(true, "The new program runs.");
        }

        var start = await Task.Run(() => ToolRunner.Capture(program, ["start", "--home", paths.Root, "--no-backup"]), ct).ConfigureAwait(false);
        if (start.ExitCode != 0)
        {
            return new HealthReport(false, $"The servers did not start: {Tail(start.Output)}");
        }

        await Task.Delay(_settle, ct).ConfigureAwait(false);
        var settings = HostSettings.LoadRequired(paths);
        var supervisor = new ServerSupervisor(paths, settings);
        var down = ServerCatalog.Enabled(settings).Where(server => !supervisor.State(server).Listening).Select(server => server.Title).ToList();
        return down.Count == 0
            ? new HealthReport(true, "Every server is running.")
            : new HealthReport(false, $"These servers stopped or do not answer: {string.Join(", ", down)}.");
    }

    public Task StopServersAsync(InstallPaths paths, CancellationToken ct) =>
        Task.Run(() => ServerStartup.StopAll(paths, HostSettings.LoadRequired(paths)), ct);

    public Task RestoreBackupAsync(InstallPaths paths, string backupFile, CancellationToken ct) =>
        Task.Run(() => DatabaseRestore.Run(HostSettings.LoadRequired(paths), backupFile), ct);

    public async Task StartServersAsync(InstallPaths paths, CancellationToken ct)
    {
        var result = await Task.Run(() => ToolRunner.Capture(paths.AppExecutable("tposeidon"), ["start", "--home", paths.Root, "--no-backup"]), ct).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new HostingException($"The earlier version did not start: {Tail(result.Output)}");
        }
    }

    public void Launch(string executable, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process.Start(info)?.Dispose();
    }

    private static string Tail(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(" ", lines.TakeLast(3));
    }
}
