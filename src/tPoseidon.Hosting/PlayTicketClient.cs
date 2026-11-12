using System.Net;
using System.Text.Json;
using System.Net.Sockets;

namespace tPoseidon.Hosting;

public static class PlayTicketClient
{
    public const string EndpointPath = "/play-ticket";
    public const string UsernameField = "username";
    public const string PasswordField = "password";

    public static Uri Endpoint(string server, int shopPort, string? webUrl)
    {
        var text = string.IsNullOrWhiteSpace(webUrl) ? $"http://{server}:{shopPort}" : webUrl.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var baseUri) || baseUri.Scheme is not ("http" or "https"))
        {
            throw new HostingException($"'{text}' is not a web address. Use something like https://play.example.org:9990.");
        }

        return new Uri(baseUri, EndpointPath);
    }

    private const int MaxReplyBytes = 4096;

    public static async Task<string?> RequestAsync(HttpClient http, Uri endpoint, string username, string password, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(endpoint);
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [UsernameField] = username,
            [PasswordField] = password,
        });
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync(endpoint, form, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
        {
            throw new PlayFailure(
                PlayFailureReason.WebPortClosed,
                $"The server's web port is closed ({endpoint.Authority} refused the connection).",
                PlayFailure.WebPortClosedHint,
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The server at {endpoint.Authority} could not be reached: {ex.Message}", "Check the address, your internet connection and that the server is running.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new PlayFailure(PlayFailureReason.ServerUnreachable, $"The server at {endpoint.Authority} did not answer in time.", "Check the address, your internet connection and that the server is running.", ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return ServiceIn(await ReadReplyAsync(response, ct).ConfigureAwait(false));
                case HttpStatusCode.Unauthorized:
                    throw new PlayFailure(PlayFailureReason.WrongPassword, "The account name or password is wrong.");
                case HttpStatusCode.TooManyRequests:
                    throw new PlayFailure(PlayFailureReason.TooManyAttempts, "Too many failed logins. Wait a few minutes and try again.");
                default:
                    throw new PlayFailure(PlayFailureReason.ServerRefused, $"The server refused the login ({(int)response.StatusCode}).");
            }
        }
    }

    public static string? ServiceIn(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(reply);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("service", out var service)
                && service.ValueKind == JsonValueKind.String)
            {
                var name = service.GetString();
                return string.Equals(name, ClientFolder.EnglishService, StringComparison.OrdinalIgnoreCase) ? ClientFolder.EnglishService
                    : string.Equals(name, ClientFolder.KoreanService, StringComparison.OrdinalIgnoreCase) ? ClientFolder.KoreanService
                    : null;
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static async Task<string> ReadReplyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength is > MaxReplyBytes)
        {
            return "";
        }

        var text = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return text.Length > MaxReplyBytes ? "" : text;
    }
}
