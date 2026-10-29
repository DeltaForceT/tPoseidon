using System.Net;
using System.Net.Sockets;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public interface IUpdateSource
{
    Task<LauncherManifest?> GetManifestAsync(Uri url, CancellationToken ct);

    Task<byte[]?> GetImageAsync(Uri url, CancellationToken ct);

    Task DownloadAsync(Uri url, Stream target, long offset, long maxBytes, IProgress<long>? received, CancellationToken ct);
}

public sealed class HttpUpdateSource : IUpdateSource
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan StallTimeout = TimeSpan.FromSeconds(30);
    private const int BufferBytes = 81920;

    private readonly HttpClient _http;

    public HttpUpdateSource(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public static HttpUpdateSource Create() => new(new HttpClient { Timeout = Timeout.InfiniteTimeSpan });

    public async Task<LauncherManifest?> GetManifestAsync(Uri url, CancellationToken ct)
    {
        var bytes = await ReadSmallAsync(url, LauncherManifest.MaxManifestBytes, ct).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            return LauncherManifest.Parse(bytes);
        }
        catch (InvalidDataException ex)
        {
            throw new PlayFailure(PlayFailureReason.UpdateFailed, $"The server's update information is not usable: {ex.Message}", "Tell the server owner.", ex);
        }
    }

    public Task<byte[]?> GetImageAsync(Uri url, CancellationToken ct) =>
        ReadSmallAsync(url, LauncherManifest.MaxBannerBytes, ct);

    public async Task DownloadAsync(Uri url, Stream target, long offset, long maxBytes, IProgress<long>? received, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (offset > 0)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, null);
        }

        using var response = await SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            target.SetLength(0);
            target.Position = 0;
            throw new PlayFailure(PlayFailureReason.ServerUnreachable, "The server could not continue the download. Starting it again.");
        }

        Check(response, url);
        if (response.StatusCode == HttpStatusCode.OK && offset > 0)
        {
            target.SetLength(0);
            target.Position = 0;
            offset = 0;
        }

        await using var body = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var buffer = new byte[BufferBytes];
        var total = offset;
        while (true)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(ct);
            stall.CancelAfter(StallTimeout);
            int read;
            try
            {
                read = await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The download from {url.Authority} stalled.");
            }
            catch (IOException ex)
            {
                throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The download from {url.Authority} was interrupted: {ex.Message}", null, ex);
            }

            if (read == 0)
            {
                return;
            }

            total += read;
            if (total > maxBytes)
            {
                throw new PlayFailure(PlayFailureReason.UpdateFailed, $"{url.AbsolutePath} is bigger than the manifest says.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received?.Report(read);
        }
    }

    private async Task<byte[]?> ReadSmallAsync(Uri url, long limit, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var response = await SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            Check(response, url);
            if (response.Content.Headers.ContentLength > limit)
            {
                throw new PlayFailure(PlayFailureReason.UpdateFailed, $"{url.AbsolutePath} is bigger than the allowed {limit} bytes.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[BufferBytes];
            int read;
            while ((read = await body.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) > 0)
            {
                if (memory.Length + read > limit)
                {
                    throw new PlayFailure(PlayFailureReason.UpdateFailed, $"{url.AbsolutePath} is bigger than the allowed {limit} bytes.");
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The server at {url.Authority} did not answer in time.", "Check the address, your internet connection and that the server is running.", null);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!;
        try
        {
            return await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            throw new PlayFailure(PlayFailureReason.WebPortClosed, $"The server's web port is closed ({url.Authority} refused the connection).", "Ask the server owner to open the web port.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The server at {url.Authority} could not be reached: {ex.Message}", "Check the address, your internet connection and that the server is running.", ex);
        }
    }

    private static void Check(HttpResponseMessage response, Uri url)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        throw response.StatusCode == HttpStatusCode.TooManyRequests
            ? new PlayFailure(PlayFailureReason.ServerBusy, $"{url.Authority} is limiting your requests.")
            : new PlayFailure(PlayFailureReason.ServerRefused, $"The server refused {url.AbsolutePath} ({(int)response.StatusCode}).");
    }
}
