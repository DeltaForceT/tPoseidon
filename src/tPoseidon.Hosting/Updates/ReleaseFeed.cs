using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace tPoseidon.Hosting.Updates;

public sealed record ReleaseAsset(string Name, Uri Url, long Size);

public sealed record ReleaseInfo(string Tag, ReleaseVersion Version, bool Prerelease, string Notes, DateTimeOffset? Published, IReadOnlyList<ReleaseAsset> Assets)
{
    public ReleaseAsset? Asset(string name) => Assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.Ordinal));
}

public sealed class UpdateFeedException : HostingException
{
    public UpdateFeedException(string message, DateTimeOffset? retryAt = null, Exception? inner = null)
        : base(message, null, inner)
    {
        RetryAt = retryAt;
    }

    public DateTimeOffset? RetryAt { get; }
}

public interface IReleaseFeed
{
    Task<IReadOnlyList<ReleaseInfo>> ListAsync(CancellationToken ct);
}

public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public const string DefaultUrl = "https://api.github.com/repos/DeltaForceT/tPoseidon/releases";
    public const int PageSize = 30;
    public const string UserAgent = "tPoseidon-updater";

    private static readonly TimeSpan DefaultRateLimitWait = TimeSpan.FromHours(1);

    private readonly HttpClient _client;
    private readonly Uri _url;
    private readonly Func<DateTimeOffset> _clock;
    private string? _etag;
    private IReadOnlyList<ReleaseInfo> _cached = [];

    public GitHubReleaseFeed(HttpClient client, Uri url, Func<DateTimeOffset>? clock = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _url = url ?? throw new ArgumentNullException(nameof(url));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<ReleaseInfo>> ListAsync(CancellationToken ct)
    {
        var separator = string.IsNullOrEmpty(_url.Query) ? "?" : "&";
        using var request = new HttpRequestMessage(HttpMethod.Get, _url + separator + "per_page=" + PageSize.ToString(CultureInfo.InvariantCulture));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue(UserAgent, "1"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (_etag is not null)
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", _etag);
        }

        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new UpdateFeedException($"The release list could not be fetched: {ex.Message}", null, ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                return _cached;
            }

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                var retryAt = RetryAt(response);
                throw new UpdateFeedException($"GitHub is limiting requests from this address. The next check is possible at {retryAt.ToLocalTime():HH:mm}.", retryAt);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateFeedException($"The release list answered with {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var releases = Parse(body);
            _etag = response.Headers.ETag?.ToString();
            _cached = releases;
            return releases;
        }
    }

    public static IReadOnlyList<ReleaseInfo> Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new UpdateFeedException("The release list is not valid JSON.", null, ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new UpdateFeedException("The release list is not a list.");
            }

            var releases = new List<ReleaseInfo>();
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || Flag(item, "draft") || !ReleaseVersion.TryParse(Text(item, "tag_name"), out var version))
                {
                    continue;
                }

                releases.Add(new ReleaseInfo(
                    Text(item, "tag_name")!,
                    version,
                    Flag(item, "prerelease"),
                    Text(item, "body") ?? "",
                    DateTimeOffset.TryParse(Text(item, "published_at"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var published) ? published : null,
                    Assets(item)));
            }

            return releases;
        }
    }

    private static List<ReleaseAsset> Assets(JsonElement release)
    {
        var assets = new List<ReleaseAsset>();
        if (!release.TryGetProperty("assets", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (var asset in list.EnumerateArray())
        {
            var name = Text(asset, "name");
            if (name is not null && Uri.TryCreate(Text(asset, "browser_download_url"), UriKind.Absolute, out var url))
            {
                assets.Add(new ReleaseAsset(name, url, asset.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0));
            }
        }

        return assets;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private DateTimeOffset RetryAt(HttpResponseMessage response)
    {
        var now = _clock();
        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            return now + delta;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            return date;
        }

        if (response.Headers.TryGetValues("X-RateLimit-Reset", out var values)
            && long.TryParse(values.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var epoch))
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return now + DefaultRateLimitWait;
    }
}
