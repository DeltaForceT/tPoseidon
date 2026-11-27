namespace tPoseidon.Hosting.Updates;

public interface IUpdateHost
{
    bool ServersRunning();

    Task SendNoticeAsync(string text, CancellationToken ct);

    Task StopServersAsync(CancellationToken ct);

    Task StartServersAsync(CancellationToken ct);

    Task<string> BackupAsync(CancellationToken ct);

    void StartHelper(int processId);
}

public sealed record UpdateOptions(int CountdownMinutes, bool Backup, string? Relaunch);

public sealed class UpdateRunner
{
    private static readonly int[] ReminderMinutes = [3, 2, 1];

    private readonly InstallPaths _paths;
    private readonly IUpdateHost _host;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly Action<string> _report;

    public UpdateRunner(InstallPaths paths, IUpdateHost host, Func<TimeSpan, CancellationToken, Task>? delay = null, Action<string>? report = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _delay = delay ?? Task.Delay;
        _report = report ?? (_ => { });
    }

    public static IReadOnlyList<int> NoticeMinutes(int countdownMinutes)
    {
        var marks = new SortedSet<int>(Comparer<int>.Create((left, right) => right.CompareTo(left)));
        if (countdownMinutes > 0)
        {
            marks.Add(countdownMinutes);
            foreach (var mark in ReminderMinutes.Where(mark => mark < countdownMinutes))
            {
                marks.Add(mark);
            }
        }

        return [.. marks];
    }

    public static string NoticeText(int minutes) =>
        minutes == 1
            ? "The server restarts in 1 minute for an update. Please finish what you are doing."
            : $"The server restarts in {minutes} minutes for an update. Please finish what you are doing.";

    public async Task HandOffAsync(StagedUpdate staged, string fromVersion, UpdateOptions options, int processId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(staged);
        ArgumentNullException.ThrowIfNull(options);
        var work = new UpdateWork(_paths);
        if (UpdateWork.Read<PendingUpdate>(work.PendingFile) is { Phase: UpdatePhase.Swapping })
        {
            throw new HostingException("An earlier update was interrupted.", "Run 'tposeidon update recover' to put the earlier version back first.", null);
        }

        var running = _host.ServersRunning();
        if (running)
        {
            await CountdownAsync(options.CountdownMinutes, work, ct).ConfigureAwait(false);
            _report("Stopping the servers.");
            await _host.StopServersAsync(ct).ConfigureAwait(false);
        }

        string? backup = null;
        if (running && options.Backup)
        {
            try
            {
                _report("Backing up the database.");
                backup = await _host.BackupAsync(ct).ConfigureAwait(false);
                work.Log($"Backup written to {backup}.");
            }
            catch (HostingException ex)
            {
                work.Log($"The backup failed: {ex.Message}");
                await _host.StartServersAsync(ct).ConfigureAwait(false);
                throw new HostingException("The update was not installed because the database could not be backed up first: " + ex.Summary, ex.Hint ?? "The servers were started again. Fix the backup, or run 'tposeidon update install --no-backup'.", ex);
            }
        }

        var pending = new PendingUpdate(fromVersion, staged.Version, UpdatePhase.Staged, running, backup, options.Relaunch, []);
        UpdateWork.Write(work.PendingFile, pending);
        work.Log($"Handing over to the update program ({fromVersion} to {staged.Version}).");
        _report("Replacing the program files; this continues in the background.");
        _host.StartHelper(processId);
    }

    private async Task CountdownAsync(int minutes, UpdateWork work, CancellationToken ct)
    {
        var marks = NoticeMinutes(minutes);
        for (var index = 0; index < marks.Count; index++)
        {
            var text = NoticeText(marks[index]);
            _report(text);
            try
            {
                await _host.SendNoticeAsync(text, ct).ConfigureAwait(false);
            }
            catch (HostingException ex)
            {
                work.Log($"The in-game notice could not be sent: {ex.Message}");
            }

            var next = index + 1 < marks.Count ? marks[index + 1] : 0;
            await _delay(TimeSpan.FromMinutes(marks[index] - next), ct).ConfigureAwait(false);
        }
    }
}
