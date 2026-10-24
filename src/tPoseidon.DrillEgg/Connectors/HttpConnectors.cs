using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using tPoseidon.DrillEgg.Config;

namespace tPoseidon.DrillEgg.Connectors;

internal sealed class HttpEndpoint
{
    public const string DefaultSecretHeader = "X-Gacha-Secret";
    public const int DefaultTimeoutSeconds = 10;

    private readonly HttpClient _client;
    private readonly string? _secretHeader;
    private readonly string? _secret;
    private readonly TimeSpan _timeout;

    public HttpEndpoint(HttpClient client, Uri url, string? secretHeader, string? secret, TimeSpan timeout)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        Url = url ?? throw new ArgumentNullException(nameof(url));
        _secretHeader = secret is null ? null : secretHeader ?? DefaultSecretHeader;
        _secret = secret;
        _timeout = timeout;
    }

    public Uri Url { get; }

    public static HttpEndpoint From(HttpClient client, Uri url, ConnectorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new HttpEndpoint(client, url, settings.SecretHeader, settings.Secret, TimeSpan.FromSeconds(settings.TimeoutSeconds ?? DefaultTimeoutSeconds));
    }

    public async Task<(HttpStatusCode Status, JsonElement? Body)> PostAsync(IReadOnlyDictionary<string, object?> body, CancellationToken ct) =>
        await PostAsync(body, true, ct).ConfigureAwait(false);

    public async Task<(HttpStatusCode Status, JsonElement? Body)> PostAsync(IReadOnlyDictionary<string, object?> body, bool readAnswer, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = JsonContent.Create(body) };
        if (_secret is not null) request.Headers.TryAddWithoutValidation(_secretHeader!, _secret);
        try
        {
            using var response = await _client.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (!readAnswer) return (response.StatusCode, null);
            var text = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(text)) return (response.StatusCode, null);
            using var document = JsonDocument.Parse(text);
            return (response.StatusCode, document.RootElement.Clone());
        }
        catch (HttpRequestException e)
        {
            throw new ConnectorException($"{Url} could not be reached: {e.Message}", e);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new ConnectorException($"{Url} did not answer within {_timeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} s.", e);
        }
        catch (JsonException e)
        {
            throw new ConnectorException($"{Url} answered with something that is not JSON: {e.Message}", e);
        }
    }

    public ConnectorException Unexpected(HttpStatusCode status) =>
        new($"{Url} answered {(int)status} {status}.");
}

internal static class JsonFields
{
    public static string? Text(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static long? Number(JsonElement body, string name)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            JsonValueKind.Null => null,
            _ => throw new ConnectorException($"The answer's \"{name}\" is not a whole number."),
        };
    }

    public static bool? Flag(JsonElement body, string name) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
}

internal sealed class HttpPlayers : IPlayerConnector
{
    private readonly HttpEndpoint _endpoint;

    public HttpPlayers(HttpEndpoint endpoint, SignInKind kind)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Kind = kind;
    }

    public SignInKind Kind { get; }

    public async Task<GachaPlayer?> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (Kind == SignInKind.Token)
        {
            body[DrawValues.Token] = request.Token;
        }
        else
        {
            body[DrawValues.Account] = request.Account;
            body[DrawValues.Password] = request.Password;
        }
        var (status, answer) = await _endpoint.PostAsync(body, ct).ConfigureAwait(false);
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound) return null;
        if (status != HttpStatusCode.OK || answer is not { } json) throw _endpoint.Unexpected(status);
        var account = JsonFields.Text(json, DrawValues.Account) ?? request.Account;
        if (string.IsNullOrWhiteSpace(account)) throw new ConnectorException($"{_endpoint.Url} accepted the sign-in but sent no \"account\".");
        return new GachaPlayer(account, JsonFields.Number(json, SqlColumns.Id), JsonFields.Text(json, DrawValues.Character));
    }
}

internal sealed class HttpCoupons : ICouponConnector
{
    public const string CouponsField = "coupons";
    public const string SpentField = "spent";

    private readonly HttpEndpoint _count;
    private readonly HttpEndpoint _spend;

    public HttpCoupons(HttpEndpoint count, HttpEndpoint spend)
    {
        _count = count ?? throw new ArgumentNullException(nameof(count));
        _spend = spend ?? throw new ArgumentNullException(nameof(spend));
    }

    public async Task<int> CountAsync(GachaPlayer player, CancellationToken ct)
    {
        var (status, answer) = await _count.PostAsync(DrawValues.Of(player), ct).ConfigureAwait(false);
        if (status != HttpStatusCode.OK || answer is not { } json) throw _count.Unexpected(status);
        return Coupons(json, _count);
    }

    public async Task<ICouponSpend?> SpendAsync(GachaPlayer player, int cost, long drawId, CancellationToken ct)
    {
        var body = DrawValues.Of(player);
        body[DrawValues.Cost] = cost;
        body[DrawValues.DrawId] = drawId;
        var (status, answer) = await _spend.PostAsync(body, ct).ConfigureAwait(false);
        if (status != HttpStatusCode.OK || answer is not { } json) throw _spend.Unexpected(status);
        var spent = JsonFields.Flag(json, SpentField) ?? throw new ConnectorException($"{_spend.Url} sent no \"{SpentField}\": true or false.");
        return spent ? new HttpSpend(Coupons(json, _spend)) : null;
    }

    private static int Coupons(JsonElement json, HttpEndpoint endpoint)
    {
        var coupons = JsonFields.Number(json, CouponsField) ?? throw new ConnectorException($"{endpoint.Url} sent no \"{CouponsField}\" count.");
        return (int)Math.Clamp(coupons, 0, int.MaxValue);
    }

    private sealed class HttpSpend : ICouponSpend
    {
        public HttpSpend(int left)
        {
            CouponsLeft = left;
        }

        public int CouponsLeft { get; }

        public SqlUnit? Unit => null;

        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

internal sealed class HttpDelivery : IDeliveryConnector
{
    private readonly HttpEndpoint _endpoint;

    public HttpDelivery(HttpEndpoint endpoint)
    {
        _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }

    public SqlDatabase? Database => null;

    public async Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct)
    {
        var (status, _) = await _endpoint.PostAsync(DrawValues.Of(delivery), false, ct).ConfigureAwait(false);
        if ((int)status is < 200 or > 299) throw _endpoint.Unexpected(status);
    }
}
