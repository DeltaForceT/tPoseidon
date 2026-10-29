using System.Net;
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

    public static async Task RequestAsync(HttpClient http, Uri endpoint, string username, string password, CancellationToken ct)
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
                "Ask the server owner to open the web port (9990 by default) and to run the item shop, which also hands out the login passes.",
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
                    return;
                case HttpStatusCode.Unauthorized:
                    throw new PlayFailure(PlayFailureReason.WrongPassword, "The account name or password is wrong.");
                case HttpStatusCode.TooManyRequests:
                    throw new PlayFailure(PlayFailureReason.TooManyAttempts, "Too many failed logins. Wait a few minutes and try again.");
                default:
                    throw new PlayFailure(PlayFailureReason.ServerRefused, $"The server refused the login ({(int)response.StatusCode}).");
            }
        }
    }
}
