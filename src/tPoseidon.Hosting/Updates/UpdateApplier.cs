namespace tPoseidon.Hosting.Updates;

public sealed record HealthReport(bool Healthy, string Detail);

public interface IApplyHost
{
    bool WaitForExit(int processId, TimeSpan timeout);

    Task<HealthReport> StartAndCheckAsync(InstallPaths paths, string expectedVersion, bool startServers, CancellationToken ct);

    Task StopServersAsync(InstallPaths paths, CancellationToken ct);

    Task RestoreBackupAsync(InstallPaths paths, string backupFile, CancellationToken ct);

    Task StartServersAsync(InstallPaths paths, CancellationToken ct);

    void Launch(string executable, IReadOnlyList<string> arguments);
}

public static class UpdateApplier
{
    public static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(60);

    public static async Task<UpdateResult> RunAsync(InstallPaths paths, int processId, IApplyHost host, Func<DateTimeOffset>? clock = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(host);
        var now = clock ?? (() => DateTimeOffset.UtcNow);
        var work = new UpdateWork(paths);
        var pending = UpdateWork.Read<PendingUpdate>(work.PendingFile);
        if (pending is null || !Directory.Exists(work.Staging))
        {
            return Finish(work, new UpdateResult(UpdateOutcome.NotStarted, "", "", "There was no staged update to install.", now()), null, host);
        }

        work.Log($"Installing {pending.ToVersion} over {pending.FromVersion}.");
        if (!host.WaitForExit(processId, CloseTimeout))
        {
            work.Log("The program that started the update did not close in time.");
            return Finish(work, new UpdateResult(UpdateOutcome.NotStarted, pending.FromVersion, pending.ToVersion, "The panel did not close in time, so nothing was changed.", now()), pending, host, clean: true);
        }

        var plan = PackageSwap.Plan(paths.Root, work.Staging);
        pending = pending with { Phase = UpdatePhase.Swapping, Swapped = plan };
        UpdateWork.Write(work.PendingFile, pending);
        try
        {
            PackageSwap.Apply(paths.Root, work.Staging, work.Previous, plan);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            work.Log($"The files could not be replaced: {ex.Message}");
            return await RollBackAsync(paths, work, pending, host, $"The files could not be replaced ({ex.Message}).", swapped: false, now, ct).ConfigureAwait(false);
        }

        work.Log("The new files are in place; starting them.");
        HealthReport health;
        try
        {
            health = await host.StartAndCheckAsync(paths, pending.ToVersion, pending.ServersWereRunning, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HostingException or IOException or InvalidOperationException)
        {
            health = new HealthReport(false, ex.Message);
        }

        if (!health.Healthy)
        {
            work.Log($"The new version did not come up: {health.Detail}");
            return await RollBackAsync(paths, work, pending, host, health.Detail, swapped: true, now, ct).ConfigureAwait(false);
        }

        work.Log("The new version is running.");
        UpdateWork.DeleteFolder(work.Previous);
        UpdateWork.DeleteFolder(work.Staging);
        UpdateWork.DeleteFolder(work.Download);
        return Finish(work, new UpdateResult(UpdateOutcome.Updated, pending.FromVersion, pending.ToVersion, $"Updated from {pending.FromVersion} to {pending.ToVersion}.", now()), pending, host);
    }

    public static async Task<UpdateResult> RecoverAsync(InstallPaths paths, IApplyHost host, Func<DateTimeOffset>? clock = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(host);
        var now = clock ?? (() => DateTimeOffset.UtcNow);
        var work = new UpdateWork(paths);
        var pending = UpdateWork.Read<PendingUpdate>(work.PendingFile);
        if (pending is not { Phase: UpdatePhase.Swapping })
        {
            return new UpdateResult(UpdateOutcome.NotStarted, "", "", "There is no interrupted update to roll back.", now());
        }

        return await RollBackAsync(paths, work, pending, host, "The update was interrupted.", swapped: true, now, ct).ConfigureAwait(false);
    }

    private static async Task<UpdateResult> RollBackAsync(InstallPaths paths, UpdateWork work, PendingUpdate pending, IApplyHost host, string reason, bool swapped, Func<DateTimeOffset> now, CancellationToken ct)
    {
        var problems = new List<string>();
        if (swapped && pending.ServersWereRunning)
        {
            await Attempt(() => host.StopServersAsync(paths, ct), "stop the new servers", problems).ConfigureAwait(false);
        }

        try
        {
            PackageSwap.Undo(paths.Root, work.Staging, work.Previous, pending.Swapped);
            work.Log("The earlier files are back.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            problems.Add($"put the earlier files back ({ex.Message})");
        }

        if (swapped && pending.BackupFile is { } backup)
        {
            await Attempt(() => host.RestoreBackupAsync(paths, backup, ct), "restore the database backup", problems).ConfigureAwait(false);
            ResetLastStartedVersion(paths, pending.FromVersion);
        }

        if (pending.ServersWereRunning && problems.Count == 0)
        {
            await Attempt(() => host.StartServersAsync(paths, ct), "start the earlier version", problems).ConfigureAwait(false);
        }

        var outcome = problems.Count == 0 ? UpdateOutcome.RolledBack : UpdateOutcome.RollbackFailed;
        var message = problems.Count == 0
            ? $"The update to {pending.ToVersion} failed and tPoseidon {pending.FromVersion} is back. Reason: {reason}"
            : $"The update to {pending.ToVersion} failed ({reason}) and going back did not finish: could not {string.Join(", could not ", problems)}. Your backup is {pending.BackupFile ?? "not available"}.";
        work.Log(message);
        if (problems.Count == 0)
        {
            UpdateWork.DeleteFolder(work.Staging);
            UpdateWork.DeleteFolder(work.Previous);
            UpdateWork.DeleteFolder(work.Download);
        }

        return Finish(work, new UpdateResult(outcome, pending.FromVersion, pending.ToVersion, message, now()), pending, host);
    }

    private static async Task Attempt(Func<Task> step, string what, List<string> problems)
    {
        try
        {
            await step().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HostingException or IOException or InvalidOperationException)
        {
            problems.Add($"{what} ({ex.Message})");
        }
    }

    private static void ResetLastStartedVersion(InstallPaths paths, string version)
    {
        try
        {
            if (HostSettings.Load(paths) is { } settings)
            {
                settings.LastStartedVersion = version;
                settings.Save(paths);
            }
        }
        catch (Exception ex) when (ex is HostingException or IOException or UnauthorizedAccessException)
        {
            new UpdateWork(paths).Log($"The saved version could not be reset: {ex.Message}");
        }
    }

    private static UpdateResult Finish(UpdateWork work, UpdateResult result, PendingUpdate? pending, IApplyHost host, bool clean = false)
    {
        if (clean)
        {
            UpdateWork.DeleteFolder(work.Staging);
        }

        UpdateWork.Write(work.ResultFile, result);
        if (result.Outcome != UpdateOutcome.RollbackFailed && File.Exists(work.PendingFile))
        {
            File.Delete(work.PendingFile);
        }

        if (pending?.Relaunch is { Length: > 0 } relaunch && File.Exists(relaunch))
        {
            host.Launch(relaunch, []);
        }

        return result;
    }
}
