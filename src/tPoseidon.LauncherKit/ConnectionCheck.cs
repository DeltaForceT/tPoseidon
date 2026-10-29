using System.Net;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.LauncherKit;

public enum ConnectionOutcome
{
    UpdateChannelFound,
    NoUpdatesPublished,
    Failed,
}

public sealed record ConnectionResult(ConnectionOutcome Outcome, string Message)
{
    public bool Reachable => Outcome != ConnectionOutcome.Failed;
}

public interface IConnectionCheck
{
    Task<ConnectionResult> CheckAsync(Uri manifestUrl, CancellationToken ct);
}

public sealed class HttpConnectionCheck : IConnectionCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient _http;

    public HttpConnectionCheck(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<ConnectionResult> CheckAsync(Uri manifestUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifestUrl);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(Timeout);
        try
        {
            using var response = await _http.GetAsync(manifestUrl, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new ConnectionResult(ConnectionOutcome.NoUpdatesPublished, "The server answers, and no launcher update is published yet. Players can still log in; publish an update when you have files for them.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ConnectionResult(ConnectionOutcome.Failed, $"The server answered {(int)response.StatusCode} at {manifestUrl}.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(limit.Token).ConfigureAwait(false);
            var manifest = LauncherManifest.Parse(bytes);
            return new ConnectionResult(ConnectionOutcome.UpdateChannelFound, $"Connected. The update channel '{manifest.Name}' lists {manifest.Files.Count} files.");
        }
        catch (InvalidDataException ex)
        {
            return new ConnectionResult(ConnectionOutcome.Failed, $"The server's update information is not usable: {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            return new ConnectionResult(ConnectionOutcome.Failed, $"{manifestUrl.Authority} could not be reached: {ex.Message}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ConnectionResult(ConnectionOutcome.Failed, $"{manifestUrl.Authority} did not answer in time.");
        }
    }
}
