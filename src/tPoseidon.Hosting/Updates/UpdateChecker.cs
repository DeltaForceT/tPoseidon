using System.Runtime.InteropServices;
using tPoseidon.Core.Common.Updates;

namespace tPoseidon.Hosting.Updates;

public sealed record UpdateOffer(ReleaseInfo Release, ReleaseAsset Package, ReleaseAsset Checksums, ReleaseAsset Signature)
{
    public ReleaseVersion Version => Release.Version;
}

public sealed record UpdateCheckResult(UpdateOffer? Offer, string? Problem, DateTimeOffset? RetryAt)
{
    public bool Failed => Problem is not null;
}

public static class ChannelPicker
{
    public static bool InChannel(ReleaseInfo release, ReleaseChannel channel)
    {
        ArgumentNullException.ThrowIfNull(release);
        return channel == ReleaseChannel.Beta || (!release.Prerelease && !release.Version.IsPrerelease);
    }

    public static UpdateOffer? Newest(IEnumerable<ReleaseInfo> releases, ReleaseChannel channel, ReleaseVersion installed, string rid)
    {
        ArgumentNullException.ThrowIfNull(releases);
        ArgumentNullException.ThrowIfNull(installed);
        foreach (var release in releases.Where(each => InChannel(each, channel) && each.Version > installed).OrderByDescending(each => each.Version))
        {
            var package = release.Asset(ReleaseAssets.PackageName(release.Version.ToString(), rid));
            var sums = release.Asset(ReleaseAssets.ChecksumFile);
            var signature = release.Asset(ReleaseAssets.SignatureFile);
            if (package is not null && sums is not null && signature is not null)
            {
                return new UpdateOffer(release, package, sums, signature);
            }
        }

        return null;
    }
}

public static class RuntimeTarget
{
    public static string Current
    {
        get
        {
            var architecture = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            if (OperatingSystem.IsWindows())
            {
                return "win-" + architecture;
            }

            return (OperatingSystem.IsMacOS() ? "osx-" : "linux-") + architecture;
        }
    }
}

public sealed class UpdateChecker
{
    private readonly IReleaseFeed _feed;
    private readonly string _rid;
    private readonly Func<DateTimeOffset> _clock;
    private DateTimeOffset? _notBefore;

    public UpdateChecker(IReleaseFeed feed, ReleaseVersion installed, string rid, Func<DateTimeOffset>? clock = null)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        Installed = installed ?? throw new ArgumentNullException(nameof(installed));
        _rid = rid ?? throw new ArgumentNullException(nameof(rid));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public ReleaseVersion Installed { get; }

    public async Task<UpdateCheckResult> CheckAsync(ReleaseChannel channel, CancellationToken ct)
    {
        if (_notBefore is { } wait && _clock() < wait)
        {
            return new UpdateCheckResult(null, $"The next check is allowed at {wait.ToLocalTime():HH:mm}.", wait);
        }

        try
        {
            var releases = await _feed.ListAsync(ct).ConfigureAwait(false);
            _notBefore = null;
            return new UpdateCheckResult(ChannelPicker.Newest(releases, channel, Installed, _rid), null, null);
        }
        catch (UpdateFeedException ex)
        {
            _notBefore = ex.RetryAt;
            return new UpdateCheckResult(null, ex.Summary, ex.RetryAt);
        }
    }
}
