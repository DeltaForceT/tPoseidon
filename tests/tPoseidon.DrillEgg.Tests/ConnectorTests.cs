using System.Net;
using System.Text.Json;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.DrillEgg.Connectors;

namespace tPoseidon.DrillEgg.Tests;

public sealed class SqlConnectorTests
{
    private static async Task<SqlDatabase> GameAsync(TempFolder folder)
    {
        var game = Fixtures.Sqlite(folder.File("game.db"));
        await Fixtures.ExecuteAsync(game, """
            CREATE TABLE accounts (uid INTEGER, login TEXT, pass TEXT, hero TEXT, link TEXT);
            INSERT INTO accounts VALUES (41, 'alice', 'secret', 'Moonlight', 'tok-1');
            CREATE TABLE wallet (uid INTEGER, coupons INTEGER);
            INSERT INTO wallet VALUES (41, 3);
            """);
        return game;
    }

    [Fact]
    public async Task SignIn_ReadsTheIdNameAndCharacterColumns()
    {
        using var folder = new TempFolder();
        var players = new SqlPlayers(await GameAsync(folder), "SELECT uid AS id, login AS account, hero AS character FROM accounts WHERE login = @account AND pass = @password", SignInKind.Password);

        var player = await players.SignInAsync(new SignInRequest("alice", "secret", null), CancellationToken.None);

        Assert.Equal(new GachaPlayer("alice", 41, "Moonlight"), player);
        Assert.Null(await players.SignInAsync(new SignInRequest("alice", "wrong", null), CancellationToken.None));
        Assert.Null(await players.SignInAsync(new SignInRequest("alice' OR '1'='1", "x' OR '1'='1", null), CancellationToken.None));
    }

    [Fact]
    public async Task SignIn_ByToken_FallsBackToTheTypedName()
    {
        using var folder = new TempFolder();
        var players = new SqlPlayers(await GameAsync(folder), "SELECT uid AS id FROM accounts WHERE link = @token", SignInKind.Token);

        Assert.Equal(SignInKind.Token, players.Kind);
        await Assert.ThrowsAsync<ConnectorException>(() => players.SignInAsync(new SignInRequest(null, null, "tok-1"), CancellationToken.None));
        Assert.Equal(41, (await players.SignInAsync(new SignInRequest("alice", null, "tok-1"), CancellationToken.None))!.AccountId);
    }

    [Fact]
    public async Task ABrokenQuery_IsAConnectorFailure()
    {
        using var folder = new TempFolder();
        var players = new SqlPlayers(await GameAsync(folder), "SELECT * FROM no_such_table", SignInKind.Password);

        var error = await Assert.ThrowsAsync<ConnectorException>(() => players.SignInAsync(new SignInRequest("a", "b", null), CancellationToken.None));
        Assert.Contains("no_such_table", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coupons_AreCountedAndSpentByRowsChanged()
    {
        using var folder = new TempFolder();
        var game = await GameAsync(folder);
        var coupons = new SqlCoupons(game, "SELECT coupons FROM wallet WHERE uid = @accountId", "UPDATE wallet SET coupons = coupons - @cost WHERE uid = @accountId AND coupons >= @cost");
        var alice = new GachaPlayer("alice", 41, null);

        Assert.Equal(3, await coupons.CountAsync(alice, CancellationToken.None));
        await using (var spend = await coupons.SpendAsync(alice, 2, 1, CancellationToken.None))
        {
            Assert.Equal(1, spend!.CouponsLeft);
            Assert.Same(game, spend.Unit!.Database);
            await spend.CommitAsync(CancellationToken.None);
        }
        Assert.Null(await coupons.SpendAsync(alice, 2, 2, CancellationToken.None));
        Assert.Equal(1, await coupons.CountAsync(alice, CancellationToken.None));
        Assert.Equal(0, await coupons.CountAsync(new GachaPlayer("nobody", 99, null), CancellationToken.None));
    }

    [Fact]
    public async Task ASpendThatAnswersANumber_UsesThatAnswer()
    {
        using var folder = new TempFolder();
        var game = await GameAsync(folder);
        var coupons = new SqlCoupons(game, "SELECT coupons FROM wallet WHERE uid = @accountId", "SELECT CASE WHEN @drawId > 10 THEN 1 ELSE 0 END");
        var alice = new GachaPlayer("alice", 41, null);

        Assert.Null(await coupons.SpendAsync(alice, 1, 5, CancellationToken.None));
        await using var spend = await coupons.SpendAsync(alice, 1, 11, CancellationToken.None);
        Assert.NotNull(spend);
    }

    [Fact]
    public async Task ADeliveryOnItsOwn_RunsInItsOwnTransaction()
    {
        using var folder = new TempFolder();
        var game = await GameAsync(folder);
        await Fixtures.ExecuteAsync(game, "CREATE TABLE gifts (uid INTEGER, item INTEGER, amount INTEGER, label TEXT, draw INTEGER, area INTEGER);");
        var delivery = new SqlDelivery(game, "INSERT INTO gifts VALUES (@accountId, @itemId, @count, @itemName, @drawId, @area)");

        await delivery.DeliverAsync(new Delivery(17, new GachaPlayer("alice", 41, null), new GachaPrize(5, 1, 4, 0, 0, 1), "Gem", 63, false), null, CancellationToken.None);

        Assert.Equal(17, await Fixtures.ScalarAsync(game, "SELECT draw FROM gifts WHERE uid = 41 AND item = 5 AND amount = 4 AND label = 'Gem' AND area = 63"));
    }

    [Fact]
    public void ParameterNames_SkipServerVariablesAndAddresses()
    {
        Assert.Equal(["account", "cost"], SqlDatabase.ParameterNames("SELECT @@ROWCOUNT, @account, a@b.c, @cost, @account"));
        Assert.Equal(["acount"], SqlDatabase.UnknownParameters("WHERE x = @acount AND y = @cost", DrawValues.CouponSpend));
    }

    [Fact]
    public void EveryProvider_HasAFactory()
    {
        foreach (var provider in SqlProviders.Names) Assert.NotNull(SqlProviders.Factory(provider).CreateConnection());
    }
}

public sealed class HttpConnectorTests
{
    private const string Secret = "s3cret";

    private static HttpEndpoint Endpoint(FakeHttp http, string path) =>
        new(new HttpClient(http), new Uri("https://game.example" + path), null, Secret, TimeSpan.FromSeconds(5));

    [Fact]
    public async Task SignIn_PostsTheAccountWithTheSecret()
    {
        var http = new FakeHttp((_, body) => body.Contains("\"good\"", StringComparison.Ordinal)
            ? FakeHttp.Json(HttpStatusCode.OK, """{"account":"Alice","id":"12","character":"Hero"}""")
            : FakeHttp.Json(HttpStatusCode.Unauthorized, "{}"));
        var players = new HttpPlayers(Endpoint(http, "/sign-in"), SignInKind.Password);

        Assert.Equal(new GachaPlayer("Alice", 12, "Hero"), await players.SignInAsync(new SignInRequest("alice", "good", null), CancellationToken.None));
        Assert.Null(await players.SignInAsync(new SignInRequest("alice", "bad", null), CancellationToken.None));
        var (request, sent) = http.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Secret, Assert.Single(request.Headers.GetValues(HttpEndpoint.DefaultSecretHeader)));
        using var json = JsonDocument.Parse(sent);
        Assert.Equal("alice", json.RootElement.GetProperty("account").GetString());
        Assert.Equal("good", json.RootElement.GetProperty("password").GetString());
    }

    [Fact]
    public async Task SignIn_ByToken_SendsOnlyTheToken()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(HttpStatusCode.OK, """{"account":"Bob"}"""));
        var players = new HttpPlayers(Endpoint(http, "/sign-in"), SignInKind.Token);

        Assert.Equal(new GachaPlayer("Bob", null, null), await players.SignInAsync(new SignInRequest(null, null, "abc"), CancellationToken.None));
        Assert.Equal("""{"token":"abc"}""", http.Requests[0].Body);
    }

    [Fact]
    public async Task AServerError_IsAConnectorFailure()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(HttpStatusCode.InternalServerError, "oops"));
        var players = new HttpPlayers(Endpoint(http, "/sign-in"), SignInKind.Password);

        var error = await Assert.ThrowsAsync<ConnectorException>(() => players.SignInAsync(new SignInRequest("a", "b", null), CancellationToken.None));
        Assert.Contains("500", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Coupons_AreCountedAndSpentByTheGameServer()
    {
        var http = new FakeHttp((request, body) => request.RequestUri!.AbsolutePath switch
        {
            "/count" => FakeHttp.Json(HttpStatusCode.OK, """{"coupons":4}"""),
            _ when body.Contains("\"cost\":2", StringComparison.Ordinal) => FakeHttp.Json(HttpStatusCode.OK, """{"spent":false,"coupons":1}"""),
            _ => FakeHttp.Json(HttpStatusCode.OK, """{"spent":true,"coupons":3}"""),
        });
        var coupons = new HttpCoupons(Endpoint(http, "/count"), Endpoint(http, "/spend"));
        var alice = new GachaPlayer("Alice", 12, null);

        Assert.Equal(4, await coupons.CountAsync(alice, CancellationToken.None));
        Assert.Null(await coupons.SpendAsync(alice, 2, 8, CancellationToken.None));
        await using var spend = await coupons.SpendAsync(alice, 1, 9, CancellationToken.None);
        Assert.Equal(3, spend!.CouponsLeft);
        Assert.Null(spend.Unit);
        using var json = JsonDocument.Parse(http.Requests[^1].Body);
        Assert.Equal(9, json.RootElement.GetProperty("drawId").GetInt64());
        Assert.Equal(12, json.RootElement.GetProperty("accountId").GetInt64());
    }

    [Fact]
    public async Task ASpendAnswerWithoutTheFlag_IsAConnectorFailure()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(HttpStatusCode.OK, """{"coupons":3}"""));
        var coupons = new HttpCoupons(Endpoint(http, "/count"), Endpoint(http, "/spend"));

        await Assert.ThrowsAsync<ConnectorException>(() => coupons.SpendAsync(new GachaPlayer("A", 1, null), 1, 1, CancellationToken.None));
    }

    [Fact]
    public async Task TheWebhook_SendsThePrize_AndTreatsAnyErrorAsNotDelivered()
    {
        var status = HttpStatusCode.NoContent;
        var http = new FakeHttp((_, _) => new HttpResponseMessage(status) { Content = new StringContent("ok") });
        var delivery = new HttpDelivery(Endpoint(http, "/deliver"));
        var won = new Delivery(31, new GachaPlayer("Alice", 12, "Hero"), new GachaPrize(8, 1, 2, 24, 1, 1), "Golden Pickaxe", 64, true);

        await delivery.DeliverAsync(won, null, CancellationToken.None);
        status = HttpStatusCode.BadGateway;
        await Assert.ThrowsAsync<ConnectorException>(() => delivery.DeliverAsync(won, null, CancellationToken.None));

        using var json = JsonDocument.Parse(http.Requests[0].Body);
        var root = json.RootElement;
        Assert.Equal(31, root.GetProperty("drawId").GetInt64());
        Assert.Equal(8, root.GetProperty("itemId").GetInt32());
        Assert.Equal("Golden Pickaxe", root.GetProperty("itemName").GetString());
        Assert.Equal(2, root.GetProperty("count").GetInt32());
        Assert.Equal(24, root.GetProperty("useHours").GetInt32());
        Assert.Equal("Hero", root.GetProperty("character").GetString());
        Assert.True(root.GetProperty("deep").GetBoolean());
    }

    [Fact]
    public async Task AnUnreachableServer_IsAConnectorFailure()
    {
        var http = new FakeHttp((_, _) => throw new HttpRequestException("refused"));
        var delivery = new HttpDelivery(Endpoint(http, "/deliver"));

        var error = await Assert.ThrowsAsync<ConnectorException>(() => delivery.DeliverAsync(new Delivery(1, new GachaPlayer("A", null, null), new GachaPrize(1, 1, 1, 0, 0, 1), "X", 61, false), null, CancellationToken.None));
        Assert.Contains("could not be reached", error.Message, StringComparison.Ordinal);
    }
}

public sealed class TposeidonConnectorTests
{
    [Fact]
    public async Task SignIn_ChecksTheEmulatorPassword()
    {
        var accounts = new InMemoryAccountStore();
        var registered = accounts.RegisterAccount("alice", PasswordHasher.Hash("correct horse"));
        var players = new TposeidonPlayers(accounts);

        Assert.Equal(new GachaPlayer("alice", registered.AccountId, null), await players.SignInAsync(new SignInRequest("alice", "correct horse", null), CancellationToken.None));
        Assert.Null(await players.SignInAsync(new SignInRequest("alice", "wrong", null), CancellationToken.None));
        Assert.Null(await players.SignInAsync(new SignInRequest("nobody", "x", null), CancellationToken.None));
        Assert.Null(await players.SignInAsync(new SignInRequest("alice", null, null), CancellationToken.None));
    }

    [Fact]
    public async Task Delivery_GoesToTheItemBoxLikeTheWebShop()
    {
        var items = new FakeChargedItems();
        var delivery = new TposeidonDelivery(items);

        await delivery.DeliverAsync(new Delivery(5, new GachaPlayer("alice", 3, null), new GachaPrize(19557, ChargedItemRules.RareClass, 2, 48, ChargedItemRules.TimedFlag, 1), "Coins", 61, false), null, CancellationToken.None);

        Assert.Equal(new ChargedItemGrant(3, ChargedItemRules.RareClass, 19557, 2, 48, ChargedItemRules.TimedFlag, 5 + ChargedItemRules.GachaSellCodeBase, ChargedItemRules.PurchaseSellType), Assert.Single(items.Grants));
    }

    [Fact]
    public async Task Delivery_NeedsAnAccountId()
    {
        var delivery = new TposeidonDelivery(new FakeChargedItems());

        await Assert.ThrowsAsync<ConnectorException>(() => delivery.DeliverAsync(new Delivery(5, new GachaPlayer("local", null, null), new GachaPrize(1, 1, 1, 0, 0, 1), "X", 61, false), null, CancellationToken.None));
    }
}
