using Microsoft.Extensions.FileProviders;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Security;
using tPoseidon.WebShop;

const string LogTag = "[webshop]";

ConsoleFileLog.EnableIfConfigured();

var options = ShopOptions.FromEnvironment();
Console.WriteLine($"{LogTag} urls {options.Urls}, libconfig {LogPaths.Scrub(options.LibConfigPath)} (exists={File.Exists(options.LibConfigPath)})");
Console.WriteLine($"{LogTag} pages {LogPaths.Scrub(options.PagesDirectory)} (exists={Directory.Exists(options.PagesDirectory)}), art {(options.ArtDirectory is null ? "not set" : LogPaths.Scrub(options.ArtDirectory))}");

var bootstrap = await PostgresStoreBootstrap.InitializeAsync().ConfigureAwait(false);
var names = File.Exists(options.LibConfigPath) ? ItemNames.Load(options.LibConfigPath) : new ItemNames([]);
Console.WriteLine($"{LogTag} {names.Count} item names loaded");

var shop = new ShopApp(
    new PostgresShopAccountStore(bootstrap.DataSource),
    new PostgresGachaStore(bootstrap.DataSource),
    new ShopSessions(),
    names,
    options.PagesDirectory,
    TimeProvider.System,
    new PostgresCashShopStore(bootstrap.DataSource),
    options.Cash);

await using var guard = ConnectionGuard.FromEnvironment(LogTag, "webshop");
Console.WriteLine(guard.Settings.Enabled
    ? $"{LogTag} [guard] on for web requests: {guard.Settings.RequestBurst} at once then {guard.Settings.RequestsPerSecond}/s per address, bodies up to {guard.Settings.MaxRequestBodyBytes} B, {guard.Settings.MaxConnections} connections; bans and the allow list are shared with the game servers"
    : $"{LogTag} [guard] off (security.enabled is false)");
guard.Start();

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(options.Urls);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxConcurrentConnections = guard.Settings.MaxConnections;
    kestrel.Limits.MaxRequestBodySize = guard.Settings.MaxRequestBodyBytes;
    kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(guard.Settings.HandshakeTimeoutSeconds);
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();

app.Use(async (context, next) =>
{
    switch (guard.AdmitRequest(context.Connection.RemoteIpAddress ?? System.Net.IPAddress.None))
    {
        case GuardRefusal.None:
            await next(context).ConfigureAwait(false);
            return;
        case GuardRefusal.Banned:
            context.Abort();
            return;
        default:
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
    }
});

app.MapMethods("/gacha.aspx", [HttpMethods.Get, HttpMethods.Post], (Delegate)shop.GachaPageAsync);
app.MapPost("/gacha/state", shop.GachaState);
app.MapPost("/gacha/draw", shop.GachaDraw);
app.MapGet("/close", ShopApp.Close);
app.MapMethods("/BuyItem.aspx", [HttpMethods.Get, HttpMethods.Post], (Delegate)shop.BuyItemPageAsync);
app.MapMethods("/GiftItem.aspx", [HttpMethods.Get, HttpMethods.Post], (Delegate)shop.GiftItemPageAsync);
app.MapMethods("/mypoint.aspx", [HttpMethods.Get, HttpMethods.Post], (Delegate)shop.MyPointAsync);
app.MapMethods("/chargepoint.aspx", [HttpMethods.Get, HttpMethods.Post], (Delegate)shop.ChargePointPageAsync);
Console.WriteLine($"{LogTag} cash: {options.Cash.Mode.ToString().ToLowerInvariant()}{(options.Cash.Mode == tPoseidon.Core.Data.CashMode.Balance ? $", accounts start with {options.Cash.StartingBalance}" : "")}");

if (Directory.Exists(options.PagesDirectory))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath(options.PagesDirectory)) });
}
if (options.ArtDirectory is { } art && Directory.Exists(art))
{
    app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath(art)), RequestPath = "/gacha/art" });
}

Console.WriteLine($"{LogTag} listening");
await app.RunAsync().ConfigureAwait(false);
