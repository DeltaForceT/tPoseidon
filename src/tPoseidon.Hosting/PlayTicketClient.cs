using System.Net;

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
        catch (HttpRequestException ex)
        {
            throw new HostingException($"The server at {endpoint.Authority} could not be reached: {ex.Message}", ex);
        }

        using (response)
        {
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    return;
                case HttpStatusCode.Unauthorized:
                    throw new HostingException("The account name or password is wrong.");
                case HttpStatusCode.TooManyRequests:
                    throw new HostingException("Too many failed logins. Wait a few minutes and try again.");
                default:
                    throw new HostingException($"The server refused the login ({(int)response.StatusCode}).");
            }
        }
    }
}
