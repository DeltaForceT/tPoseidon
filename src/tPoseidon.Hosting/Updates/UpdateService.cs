using tPoseidon.Core.Common;

namespace tPoseidon.Hosting.Updates;

public sealed class UpdateService
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromHours(6);

    private readonly InstallPaths _paths;
    private readonly HostSettings _settings;
    private readonly HttpClient _http;
    private readonly UpdateChecker _checker;
    private readonly string _publicKey;

    public UpdateService(InstallPaths paths, HostSettings settings, HttpClient http, IReleaseFeed feed, string installedVersion, string publicKey, string? rid = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        InstalledVersion = installedVersion;
        _checker = new UpdateChecker(feed, ReleaseVersion.TryParse(installedVersion, out var parsed) ? parsed : new ReleaseVersion(0, 0, 0, ""), rid ?? RuntimeTarget.Current);
    }

    public string InstalledVersion { get; }

    public UpdateSettings Settings => _settings.Updates;

    public static UpdateService Create(InstallPaths paths, HostSettings settings, HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var url = settings.Updates.FeedUrl.Length > 0 ? settings.Updates.FeedUrl : GitHubReleaseFeed.DefaultUrl;
        return new UpdateService(paths, settings, http, new GitHubReleaseFeed(http, new Uri(url)), BuildIdentity.Current.Version, ReleaseKey.PublicKey);
    }

    public Task<UpdateCheckResult> CheckAsync(CancellationToken ct) => _checker.CheckAsync(_settings.Updates.Channel, ct);

    public async Task InstallAsync(UpdateOffer offer, UpdateOptions options, IUpdateHost host, int processId, IProgress<double>? progress, Action<string>? report, Func<TimeSpan, CancellationToken, Task>? delay, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(host);
        if (!File.Exists(_paths.AppExecutable("tposeidon")))
        {
            throw new HostingException($"{_paths.Root} has no installed program (app/{InstallPaths.Executable("tposeidon")}), so it cannot be updated in place.", "Update the package you unpacked by hand, or start the panel from the installed folder.", null);
        }

        var work = new UpdateWork(_paths);
        report?.Invoke($"Downloading and checking tPoseidon {offer.Version}.");
        var staged = await new UpdateStager(_http, work, _publicKey).StageAsync(offer, progress, ct).ConfigureAwait(false);
        await new UpdateRunner(_paths, host, delay, report).HandOffAsync(staged, InstalledVersion, options, processId, ct).ConfigureAwait(false);
    }

    public UpdateOptions OptionsFor(bool backup, string? relaunch, int? countdownMinutes = null) =>
        new(_settings.Updates.CanSendNotice ? countdownMinutes ?? _settings.Updates.CountdownMinutes : 0, backup, relaunch);

    public static UpdateResult? LastResult(InstallPaths paths) => UpdateWork.Read<UpdateResult>(new UpdateWork(paths).ResultFile);

    public static UpdateResult? TakeUnseenResult(InstallPaths paths)
    {
        var work = new UpdateWork(paths);
        var result = UpdateWork.Read<UpdateResult>(work.ResultFile);
        if (result is null || result.Acknowledged)
        {
            return null;
        }

        UpdateWork.Write(work.ResultFile, result with { Acknowledged = true });
        return result;
    }

    public static bool AutomaticDue(UpdateSettings settings, DateTime localNow, DateTime? lastAttempt)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Mode == UpdateMode.Automatic
            && localNow.Hour == settings.AutomaticHour
            && lastAttempt?.Date != localNow.Date;
    }

    public static bool FailedBefore(InstallPaths paths, string version) =>
        LastResult(paths) is { Outcome: UpdateOutcome.RolledBack or UpdateOutcome.RollbackFailed } last
        && string.Equals(last.ToVersion, version, StringComparison.Ordinal);
}
