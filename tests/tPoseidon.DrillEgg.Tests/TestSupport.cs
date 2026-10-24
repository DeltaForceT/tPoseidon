using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Config;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg.Tests;

internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "drill-egg-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
    }
}

internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string, HttpResponseMessage> _answer;

    public FakeHttp(Func<HttpRequestMessage, string, HttpResponseMessage> answer)
    {
        _answer = answer;
    }

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add((request, body));
        return _answer(request, body);
    }
}

internal sealed class FakeGachaStore : IGachaStore
{
    public int Coupons { get; set; }

    public List<GachaDrawRequest> Draws { get; } = [];

    public GachaPrize Prize { get; init; } = new(5001, 1, 2, 0, 0, 1);

    public int CouponCount(uint accountId) => Coupons;

    public void GrantCoupons(uint accountId, int count, GachaCouponSource source) => Coupons += count;

    public IReadOnlyList<GachaPrize> Pool(short area, bool deep) => [Prize];

    public GachaDrawResult Draw(GachaDrawRequest request, Func<IReadOnlyList<GachaPrize>, GachaPrize> pick)
    {
        Draws.Add(request);
        var cost = GachaRules.CouponsFor(request.Deep);
        if (Coupons < cost) return new GachaDrawResult(GachaDrawOutcome.NotEnoughCoupons, null, 0, Coupons);
        Coupons -= cost;
        return new GachaDrawResult(GachaDrawOutcome.Won, pick([Prize]), 900, Coupons);
    }
}

internal sealed class FakeChargedItems : IChargedItemStore
{
    public List<ChargedItemGrant> Grants { get; } = [];

    public IReadOnlyList<ChargedItem> ListWaiting(uint accountId) => [];

    public ulong Grant(ChargedItemGrant grant)
    {
        Grants.Add(grant);
        return (ulong)Grants.Count;
    }

    public ChargedItemMoveResult MoveToInventory(ChargedItemMove move) => new(null, ChargedItemRules.NotWaiting);
}

internal sealed class FailingDelivery : IDeliveryConnector
{
    public FailingDelivery(SqlDatabase? database)
    {
        Database = database;
    }

    public SqlDatabase? Database { get; }

    public bool Fail { get; set; } = true;

    public List<Delivery> Delivered { get; } = [];

    public Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct)
    {
        if (Fail) throw new ConnectorException("the game server is down");
        Delivered.Add(delivery);
        return Task.CompletedTask;
    }
}

internal static class Fixtures
{
    public const string Prizes = """
        "prizes": [
          { "id": 1, "name": "Small Healing Potion", "weight": 3 },
          { "id": 2, "name": "Crystal Egg", "weight": 1, "rare": true }
        ]
        """;

    public static LoadedSettings Settings(string folder, string body) =>
        SettingsLoader.Parse("{" + body + "}", folder, "test.json");

    public static async Task<LocalStore> StoreAsync(TempFolder folder) =>
        await LocalStore.OpenAsync(folder.File("drill-egg.db"), TimeProvider.System, CancellationToken.None).ConfigureAwait(false);

    public static SqlDatabase Sqlite(string file) =>
        new("game", SqliteFactory.Instance, new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());

    public static async Task ExecuteAsync(SqlDatabase database, string sql)
    {
        await using var connection = await database.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public static async Task<long> ScalarAsync(SqlDatabase database, string sql)
    {
        await using var connection = await database.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync().ConfigureAwait(false), System.Globalization.CultureInfo.InvariantCulture);
    }
}
