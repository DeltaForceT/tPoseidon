using System.Globalization;
using Npgsql;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Game.Gacha;
using tPoseidon.DrillEgg.Art;
using tPoseidon.DrillEgg.Config;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Draws;
using tPoseidon.DrillEgg.Prizes;
using tPoseidon.DrillEgg.Storage;

namespace tPoseidon.DrillEgg;

internal sealed class DrillEggServices : IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly NpgsqlDataSource? _tposeidon;

    private DrillEggServices(
        LoadedSettings loaded,
        LocalStore store,
        IPlayerConnector players,
        IDrawEngine engine,
        DeliveryRetrier? retrier,
        LocalCoupons? localCoupons,
        PrizeCatalog catalog,
        ArtSetup art,
        IReadOnlyList<string> warnings,
        HttpClient http,
        NpgsqlDataSource? tposeidon)
    {
        Loaded = loaded;
        Store = store;
        Players = players;
        Engine = engine;
        Retrier = retrier;
        LocalCoupons = localCoupons;
        Catalog = catalog;
        Pictures = new PrizePictures(loaded.PicturesFolder, catalog);
        Art = art;
        Warnings = warnings;
        _http = http;
        _tposeidon = tposeidon;
    }

    public LoadedSettings Loaded { get; }

    public LocalStore Store { get; }

    public IPlayerConnector Players { get; }

    public IDrawEngine Engine { get; }

    public DeliveryRetrier? Retrier { get; }

    public LocalCoupons? LocalCoupons { get; }

    public PrizeCatalog Catalog { get; }

    public PrizePictures Pictures { get; }

    public ArtSetup Art { get; }

    public IReadOnlyList<string> Warnings { get; }

    public TimeSpan RetryEvery => TimeSpan.FromSeconds(Loaded.Settings.Delivery.RetrySeconds ?? DeliveryRetrier.DefaultRetrySeconds);

    public static async Task<DrillEggServices> CreateAsync(LoadedSettings loaded, ArtExtractor extract, TextWriter log, TimeProvider clock, HttpMessageHandler? handler, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        ArgumentNullException.ThrowIfNull(log);
        var settings = loaded.Settings;
        var databases = settings.Databases.ToDictionary(pair => pair.Key, pair => SqlDatabase.From(pair.Key, pair.Value), StringComparer.Ordinal);
        var store = await LocalStore.OpenAsync(loaded.DataFile, clock, ct).ConfigureAwait(false);
        var http = handler is null ? new HttpClient() : new HttpClient(handler, disposeHandler: false);
        NpgsqlDataSource? tposeidon = null;
        if (settings.Tposeidon is { } emulator && UsesTposeidon(settings))
        {
            ActiveSchemaProvider.Set(emulator.Schema);
            tposeidon = NpgsqlDataSource.Create(emulator.Connection);
        }
        var catalog = PrizeCatalog.From(settings);
        var players = PlayersFrom(settings.Players, databases, http, tposeidon);
        LocalCoupons? localCoupons = null;
        IDrawEngine engine;
        DeliveryRetrier? retrier = null;
        if (settings.Coupons.Connector == ConnectorNames.Tposeidon)
        {
            engine = new StoreDrawEngine(new PostgresGachaStore(tposeidon!), catalog, store, log);
        }
        else
        {
            var coupons = CouponsFrom(settings.Coupons, databases, http, store);
            localCoupons = coupons as LocalCoupons;
            var pipeline = new PipelineDrawEngine(coupons, DeliveryFrom(settings.Delivery, databases, http, tposeidon), catalog, store, GachaPicker.Pick, log);
            engine = pipeline;
            retrier = new DeliveryRetrier(store, pipeline, log);
        }
        var art = new ArtSetup(loaded.ArtFolder, loaded.SwfPath, extract, log);
        return new DrillEggServices(loaded, store, players, engine, retrier, localCoupons, catalog, art, WarningsFor(settings), http, tposeidon);
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        if (_tposeidon is not null) await _tposeidon.DisposeAsync().ConfigureAwait(false);
    }

    private static bool UsesTposeidon(DrillEggSettings settings) =>
        new[] { settings.Players, settings.Coupons, settings.Delivery }.Any(c => c.Connector == ConnectorNames.Tposeidon);

    private static IPlayerConnector PlayersFrom(ConnectorSettings players, Dictionary<string, SqlDatabase> databases, HttpClient http, NpgsqlDataSource? tposeidon) => players.Connector switch
    {
        ConnectorNames.Local when players.Mode == SettingsValidator.NamedPlayerMode => LocalPlayers.Named(),
        ConnectorNames.Local => LocalPlayers.Single(players.Name),
        ConnectorNames.Sql => new SqlPlayers(databases[players.Database!], players.Query!, SignInOf(players)),
        ConnectorNames.Http => new HttpPlayers(HttpEndpoint.From(http, players.Url!, players), SignInOf(players)),
        ConnectorNames.Tposeidon => new TposeidonPlayers(new PostgresAccountStore(tposeidon!)),
        _ => throw new SettingsException($"players.connector \"{players.Connector}\" is not known."),
    };

    private static ICouponConnector CouponsFrom(ConnectorSettings coupons, Dictionary<string, SqlDatabase> databases, HttpClient http, LocalStore store) => coupons.Connector switch
    {
        ConnectorNames.Free => new FreeCoupons(),
        ConnectorNames.Local => new LocalCoupons(store, coupons.StartingCoupons ?? LocalCoupons.DefaultStartingCoupons),
        ConnectorNames.Sql => new SqlCoupons(databases[coupons.Database!], coupons.Count!, coupons.Spend!),
        ConnectorNames.Http => new HttpCoupons(HttpEndpoint.From(http, coupons.CountUrl!, coupons), HttpEndpoint.From(http, coupons.SpendUrl!, coupons)),
        _ => throw new SettingsException($"coupons.connector \"{coupons.Connector}\" is not known."),
    };

    private static IDeliveryConnector DeliveryFrom(ConnectorSettings delivery, Dictionary<string, SqlDatabase> databases, HttpClient http, NpgsqlDataSource? tposeidon) => delivery.Connector switch
    {
        ConnectorNames.Local => new LocalDelivery(),
        ConnectorNames.Sql => new SqlDelivery(databases[delivery.Database!], delivery.Deliver!),
        ConnectorNames.Http => new HttpDelivery(HttpEndpoint.From(http, delivery.Url!, delivery)),
        ConnectorNames.Tposeidon => new TposeidonDelivery(new PostgresChargedItemStore(tposeidon!)),
        _ => throw new SettingsException($"delivery.connector \"{delivery.Connector}\" is not known."),
    };

    private static SignInKind SignInOf(ConnectorSettings players) =>
        players.SignIn == SettingsValidator.TokenSignIn ? SignInKind.Token : SignInKind.Password;

    private static List<string> WarningsFor(DrillEggSettings settings)
    {
        var warnings = new List<string>();
        void Check(string section, string? sql, IReadOnlyList<string> known)
        {
            if (sql is null) return;
            foreach (var name in SqlDatabase.UnknownParameters(sql, known))
            {
                warnings.Add(string.Create(CultureInfo.InvariantCulture, $"{section} uses @{name}, which the Drill Egg does not fill in. That is fine for a variable the query declares itself; otherwise check the spelling. Values it fills in: {string.Join(", ", known.Select(k => "@" + k))}."));
            }
        }
        if (settings.Players.Connector == ConnectorNames.Sql) Check("players.query", settings.Players.Query, DrawValues.SignIn);
        if (settings.Coupons.Connector == ConnectorNames.Sql)
        {
            Check("coupons.count", settings.Coupons.Count, DrawValues.CouponCount);
            Check("coupons.spend", settings.Coupons.Spend, DrawValues.CouponSpend);
        }
        if (settings.Delivery.Connector == ConnectorNames.Sql) Check("delivery.deliver", settings.Delivery.Deliver, DrawValues.Deliver);
        if (settings.Delivery.Connector == ConnectorNames.Sql && settings.Coupons.Connector == ConnectorNames.Sql && settings.Delivery.Database != settings.Coupons.Database)
        {
            warnings.Add("coupons and delivery use different databases, so a prize is delivered after its coupons are spent; a failed delivery is kept and retried.");
        }
        return warnings;
    }
}
