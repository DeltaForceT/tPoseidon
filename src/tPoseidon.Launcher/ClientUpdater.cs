using System.Globalization;
using System.Text.Json;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public sealed record UpdatePlan(IReadOnlyList<ManifestFile> Changes)
{
    public static UpdatePlan Empty { get; } = new([]);

    public bool IsEmpty => Changes.Count == 0;

    public long Bytes => Changes.Sum(file => file.Size);
}

public sealed record UpdateProgress(int FileNumber, int FileCount, long DoneBytes, long TotalBytes, string Current);

public sealed class ClientUpdater
{
    public const int Attempts = 3;

    private const string BackupRecord = "backup.json";
    private static readonly JsonSerializerOptions RecordOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly TimeSpan _retryDelay;

    public ClientUpdater(IUpdateSource source, TimeSpan retryDelay)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        _retryDelay = retryDelay;
    }

    public IUpdateSource Source { get; }

    public static UpdatePlan Plan(string clientFolder, LauncherManifest manifest)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        ArgumentNullException.ThrowIfNull(manifest);
        var changes = new List<ManifestFile>();
        foreach (var file in manifest.Files)
        {
            var local = new FileInfo(ClientPath.Resolve(clientFolder, file.Path));
            if (!local.Exists || local.Length != file.Size || !string.Equals(ManifestBuilder.Hash(local.FullName), file.Sha256, StringComparison.Ordinal))
            {
                changes.Add(file);
            }
        }

        return new UpdatePlan(changes);
    }

    public async Task ApplyAsync(string clientFolder, UpdatePlan plan, Func<string, Uri> fileUrl, IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fileUrl);
        if (plan.IsEmpty)
        {
            return;
        }

        var work = Path.Combine(clientFolder, ClientPath.WorkFolder);
        try
        {
            await StageAsync(work, plan, fileUrl, progress, ct).ConfigureAwait(false);
            Swap(clientFolder, work, plan);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not update the client folder: {ex.Message}", "Move the client to a folder you can write to (not Program Files), or give your account write access to it.", ex);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.UpdateFailed, $"Could not update the client folder: {ex.Message}", "Close the game client and any program using the client folder, then try again.", ex);
        }
    }

    public async Task StageAsync(string work, UpdatePlan plan, Func<string, Uri> fileUrl, IProgress<UpdateProgress>? progress, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(work);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(fileUrl);
        var done = 0L;
        for (var i = 0; i < plan.Changes.Count; i++)
        {
            var file = plan.Changes[i];
            var number = i + 1;
            var baseline = done;
            var report = progress is null ? null : new InlineProgress<long>(received => progress.Report(new UpdateProgress(number, plan.Changes.Count, baseline + received, plan.Bytes, file.Path)));
            await DownloadAsync(work, file, fileUrl(file.Path), report, ct).ConfigureAwait(false);
            done += file.Size;
            progress?.Report(new UpdateProgress(i + 1, plan.Changes.Count, done, plan.Bytes, file.Path));
        }
    }

    public static bool StagedFilesMatch(string work, UpdatePlan plan)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(work);
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Changes.All(file => File.Exists(ClientPath.Resolve(work, file.Path) + ".part") && Verified(ClientPath.Resolve(work, file.Path) + ".part", file));
    }

    public static bool Undo(string clientFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        var backups = Path.Combine(clientFolder, ClientPath.BackupFolder);
        if (!Directory.Exists(backups))
        {
            return false;
        }

        var latest = Directory.EnumerateDirectories(backups).Order(StringComparer.Ordinal).LastOrDefault();
        if (latest is null)
        {
            return false;
        }

        var record = JsonSerializer.Deserialize<Backup>(File.ReadAllText(Path.Combine(latest, BackupRecord)), RecordOptions)
            ?? throw new HostingException($"The backup in {latest} is unreadable.");
        Restore(clientFolder, latest, record);
        Directory.Delete(latest, recursive: true);
        return true;
    }

    private async Task DownloadAsync(string work, ManifestFile file, Uri url, IProgress<long>? progress, CancellationToken ct)
    {
        var part = ClientPath.Resolve(work, file.Path) + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(part)!);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var offset = File.Exists(part) ? new FileInfo(part).Length : 0;
                if (offset > file.Size)
                {
                    File.Delete(part);
                    offset = 0;
                }

                await using (var stream = new FileStream(part, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    stream.Seek(offset, SeekOrigin.Begin);
                    if (offset < file.Size || file.Size == 0)
                    {
                        await Source.DownloadAsync(url, stream, offset, file.Size, progress, ct).ConfigureAwait(false);
                    }
                }

                if (Verified(part, file))
                {
                    return;
                }

                File.Delete(part);
                throw new PlayFailure(PlayFailureReason.UpdateFailed, $"{file.Path} did not match the published checksum after downloading.", "The file will be fetched again.", null);
            }
            catch (PlayFailure failure) when (attempt < Attempts && failure.Reason is PlayFailureReason.ServerUnreachable or PlayFailureReason.ServerBusy or PlayFailureReason.UpdateFailed)
            {
                await Task.Delay(_retryDelay, ct).ConfigureAwait(false);
            }
        }
    }

    private static bool Verified(string path, ManifestFile file) =>
        new FileInfo(path).Length == file.Size && string.Equals(ManifestBuilder.Hash(path), file.Sha256, StringComparison.Ordinal);

    public static void Swap(string clientFolder, string work, UpdatePlan plan)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
        var backup = Path.Combine(clientFolder, ClientPath.BackupFolder, stamp);
        Directory.CreateDirectory(backup);
        var added = new List<string>();
        var replaced = new List<string>();
        foreach (var file in plan.Changes)
        {
            var target = ClientPath.Resolve(clientFolder, file.Path);
            if (File.Exists(target))
            {
                replaced.Add(file.Path);
            }
            else
            {
                added.Add(file.Path);
            }
        }

        var record = new Backup(added, replaced);
        File.WriteAllText(Path.Combine(backup, BackupRecord), JsonSerializer.Serialize(record, RecordOptions));
        try
        {
            foreach (var file in plan.Changes)
            {
                var target = ClientPath.Resolve(clientFolder, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (replaced.Contains(file.Path, StringComparer.Ordinal))
                {
                    var saved = ClientPath.Resolve(backup, file.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Move(target, saved, overwrite: true);
                }

                File.Move(ClientPath.Resolve(work, file.Path) + ".part", target, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Restore(clientFolder, backup, record);
            Directory.Delete(backup, recursive: true);
            throw;
        }

        Directory.Delete(work, recursive: true);
    }

    public static void KeepOnlyNewestBackup(string clientFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        var backups = Path.Combine(clientFolder, ClientPath.BackupFolder);
        if (!Directory.Exists(backups))
        {
            return;
        }

        foreach (var old in Directory.EnumerateDirectories(backups).Order(StringComparer.Ordinal).SkipLast(1))
        {
            Directory.Delete(old, recursive: true);
        }
    }

    public static bool HasBackup(string clientFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientFolder);
        var backups = Path.Combine(clientFolder, ClientPath.BackupFolder);
        return Directory.Exists(backups) && Directory.EnumerateDirectories(backups).Any();
    }

    private static void Restore(string clientFolder, string backup, Backup record)
    {
        foreach (var path in record.Added)
        {
            var target = ClientPath.Resolve(clientFolder, path);
            if (File.Exists(target))
            {
                File.Delete(target);
            }
        }

        foreach (var path in record.Replaced)
        {
            var saved = ClientPath.Resolve(backup, path);
            if (File.Exists(saved))
            {
                var target = ClientPath.Resolve(clientFolder, path);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(saved, target, overwrite: true);
            }
        }
    }

    private sealed record Backup(IReadOnlyList<string> Added, IReadOnlyList<string> Replaced);
}

internal sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;

    public InlineProgress(Action<T> report)
    {
        _report = report;
    }

    public void Report(T value) => _report(value);
}
