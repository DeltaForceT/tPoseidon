using System.Diagnostics;
using System.Text.Json;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public sealed record LauncherStage(string Version, string Executable, IReadOnlyList<ManifestFile> Files)
{
    public const string FileName = "launcher-stage.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public UpdatePlan Plan => new(Files);

    public static LauncherStage? Read(string work)
    {
        var path = Path.Combine(work, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var stage = JsonSerializer.Deserialize<LauncherStage>(File.ReadAllText(path), Options);
            return stage is { Files: not null, Version: not null, Executable: not null } ? stage : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(string work)
    {
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, FileName), JsonSerializer.Serialize(this, Options));
    }
}

public sealed record LauncherUpdateFailure(string Version, string Reason, bool Acknowledged = false)
{
    public const string FileName = ".update-failed.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static LauncherUpdateFailure? Read(string launcherFolder)
    {
        var path = Path.Combine(launcherFolder, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var failure = JsonSerializer.Deserialize<LauncherUpdateFailure>(File.ReadAllText(path), Options);
            return failure is { Version: not null, Reason: not null } ? failure : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Write(string launcherFolder) =>
        File.WriteAllText(Path.Combine(launcherFolder, FileName), JsonSerializer.Serialize(this, Options));

    public static void Clear(string launcherFolder) => File.Delete(Path.Combine(launcherFolder, FileName));
}

public interface ISwapHost
{
    bool WaitForExit(int processId, TimeSpan timeout);

    void Start(string executablePath, IReadOnlyList<string> arguments);
}

public sealed class ProcessSwapHost : ISwapHost
{
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

    public void Start(string executablePath, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(executablePath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executablePath)! };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process.Start(info)?.Dispose();
    }
}

public sealed record SwapRequest(int ProcessId, string LauncherFolder, string Executable, string CurrentVersion);

public enum SwapResult
{
    Updated,
    Failed,
    WentBack,
}

public static class LauncherSwap
{
    public const string ApplyArgument = "--apply-update";
    public const string GoBackArgument = "--go-back";
    public const string HelperFolderPrefix = "tposeidon-swap-";
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(60);

    private const int ArgumentCount = 5;

    public static SwapResult Apply(SwapRequest request, ISwapHost host)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(host);
        var work = Path.Combine(request.LauncherFolder, ClientPath.WorkFolder);
        var stage = LauncherStage.Read(work);
        if (stage is null)
        {
            return Fail(request, host, request.CurrentVersion, "The downloaded launcher files are missing.", work);
        }

        if (!host.WaitForExit(request.ProcessId, CloseTimeout))
        {
            return Fail(request, host, stage.Version, "The launcher did not close in time.", work, restart: false);
        }

        if (!ClientUpdater.StagedFilesMatch(work, stage.Plan))
        {
            return Fail(request, host, stage.Version, "The downloaded launcher files did not match their checksums.", work);
        }

        try
        {
            ClientUpdater.Swap(request.LauncherFolder, work, stage.Plan);
            ClientUpdater.KeepOnlyNewestBackup(request.LauncherFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail(request, host, stage.Version, $"The launcher files could not be replaced: {ex.Message}", work);
        }

        host.Start(Path.Combine(request.LauncherFolder, stage.Executable), []);
        return SwapResult.Updated;
    }

    public static SwapResult GoBack(SwapRequest request, ISwapHost host)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(host);
        if (!host.WaitForExit(request.ProcessId, CloseTimeout))
        {
            return Fail(request, host, request.CurrentVersion, "The launcher did not close in time.", null, restart: false);
        }

        try
        {
            if (!ClientUpdater.Undo(request.LauncherFolder))
            {
                return Fail(request, host, request.CurrentVersion, "There is no earlier launcher to go back to.", null);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or HostingException)
        {
            return Fail(request, host, request.CurrentVersion, $"The earlier launcher could not be restored: {ex.Message}", null);
        }

        new LauncherUpdateFailure(request.CurrentVersion, "You went back to the earlier launcher, so this version was not installed again.", false).Write(request.LauncherFolder);
        host.Start(Path.Combine(request.LauncherFolder, request.Executable), []);
        return SwapResult.WentBack;
    }

    public static bool TryRun(string[] args, ISwapHost host, out SwapResult result)
    {
        result = SwapResult.Failed;
        if (args.Length != ArgumentCount || args[0] is not (ApplyArgument or GoBackArgument) || !int.TryParse(args[1], out var processId))
        {
            return false;
        }

        var request = new SwapRequest(processId, args[2], args[3], args[4]);
        result = args[0] == ApplyArgument ? Apply(request, host) : GoBack(request, host);
        return true;
    }

    public static IReadOnlyList<string> Arguments(string mode, SwapRequest request) =>
        [mode, request.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.LauncherFolder, request.Executable, request.CurrentVersion];

    private static SwapResult Fail(SwapRequest request, ISwapHost host, string version, string reason, string? work, bool restart = true)
    {
        new LauncherUpdateFailure(version, reason, false).Write(request.LauncherFolder);
        if (work is not null && Directory.Exists(work))
        {
            Directory.Delete(work, recursive: true);
        }

        if (restart)
        {
            host.Start(Path.Combine(request.LauncherFolder, request.Executable), []);
        }

        return SwapResult.Failed;
    }
}

public sealed class LauncherSelfUpdater
{
    private readonly ClientUpdater _updater;
    private readonly ISwapHost _host;
    private readonly string _executable;

    public LauncherSelfUpdater(string launcherFolder, string currentVersion, string executable, ClientUpdater updater, ISwapHost host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(currentVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        LauncherFolder = Path.GetFullPath(launcherFolder);
        CurrentVersion = currentVersion;
        _executable = executable;
        _updater = updater ?? throw new ArgumentNullException(nameof(updater));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public string LauncherFolder { get; }

    public string CurrentVersion { get; }

    public string WorkFolder => Path.Combine(LauncherFolder, ClientPath.WorkFolder);

    public bool CanGoBack => ClientUpdater.HasBackup(LauncherFolder);

    public LauncherUpdateFailure? TakeUnseenFailure()
    {
        var failure = LauncherUpdateFailure.Read(LauncherFolder);
        if (failure is null || failure.Acknowledged)
        {
            return null;
        }

        (failure with { Acknowledged = true }).Write(LauncherFolder);
        return failure;
    }

    public bool Wants(LauncherRelease release)
    {
        ArgumentNullException.ThrowIfNull(release);
        if (!LauncherVersions.IsNewer(release.Version, CurrentVersion))
        {
            return false;
        }

        var failure = LauncherUpdateFailure.Read(LauncherFolder);
        return failure is null || !string.Equals(failure.Version, release.Version, StringComparison.Ordinal);
    }

    public async Task<LauncherStage?> StageAsync(LauncherRelease release, Func<string, Uri> fileUrl, IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(fileUrl);
        var existing = LauncherStage.Read(WorkFolder);
        if (existing is not null && string.Equals(existing.Version, release.Version, StringComparison.Ordinal) && ClientUpdater.StagedFilesMatch(WorkFolder, existing.Plan))
        {
            return existing;
        }

        if (Directory.Exists(WorkFolder))
        {
            Directory.Delete(WorkFolder, recursive: true);
        }

        var reserved = release.Files.FirstOrDefault(file => file.Path.StartsWith('.'));
        if (reserved is not null)
        {
            throw new PlayFailure(PlayFailureReason.UpdateFailed, $"The launcher update lists a file the launcher keeps for itself ({reserved.Path}).", "Tell the server owner.", null);
        }

        var changes = release.Files.Where(Differs).ToList();
        if (changes.Count == 0)
        {
            return null;
        }

        var stage = new LauncherStage(release.Version, release.Executable, changes);
        await _updater.StageAsync(WorkFolder, stage.Plan, fileUrl, progress, ct).ConfigureAwait(false);
        stage.Write(WorkFolder);
        return stage;
    }

    public void Restart(LauncherStage stage) => Start(LauncherSwap.ApplyArgument, stage.Version);

    public void GoBack() => Start(LauncherSwap.GoBackArgument, CurrentVersion);

    private bool Differs(ManifestFile file)
    {
        var local = new FileInfo(ClientPath.Resolve(LauncherFolder, file.Path));
        return !local.Exists || local.Length != file.Size || !string.Equals(ManifestBuilder.Hash(local.FullName), file.Sha256, StringComparison.Ordinal);
    }

    private void Start(string mode, string version)
    {
        var helper = HelperCopy.Create(LauncherFolder, _executable);
        var request = new SwapRequest(Environment.ProcessId, LauncherFolder, _executable, version);
        _host.Start(helper, LauncherSwap.Arguments(mode, request));
    }
}

public static class HelperCopy
{
    public static string Create(string launcherFolder, string executable)
    {
        var target = Path.Combine(Path.GetTempPath(), LauncherSwap.HelperFolderPrefix + Guid.NewGuid().ToString("N"));
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(launcherFolder, "*", options))
        {
            var relative = Path.GetRelativePath(launcherFolder, file);
            if (relative.StartsWith('.'))
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }

        return Path.Combine(target, executable);
    }

    public static int RemoveFinished()
    {
        var remaining = 0;
        foreach (var folder in Directory.EnumerateDirectories(Path.GetTempPath(), LauncherSwap.HelperFolderPrefix + "*"))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                remaining++;
            }
        }

        return remaining;
    }
}
