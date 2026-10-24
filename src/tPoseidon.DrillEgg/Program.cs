using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.Gacha.Art;
using tPoseidon.DrillEgg;
using tPoseidon.DrillEgg.Config;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Web;

const string LogTag = "[drill-egg]";
const int HistoryRows = 50;

var arguments = new List<string>(args);
var configPath = SettingsLoader.DefaultFileName;
var configAt = arguments.IndexOf("--config");
if (configAt >= 0)
{
    if (configAt + 1 >= arguments.Count) return Usage();
    configPath = arguments[configAt + 1];
    arguments.RemoveRange(configAt, 2);
}
var command = arguments.Count == 0 ? "run" : arguments[0].ToLowerInvariant();

switch (command)
{
    case "extract":
        if (arguments.Count != 3) return Usage();
        return ExtractCommand.Run(arguments[1], arguments[2], Console.Out, Console.Error);
    case "run":
    case "coupons":
    case "prizes":
    case "history":
        break;
    default:
        return Usage();
}

LoadedSettings loaded;
try
{
    loaded = SettingsLoader.Load(configPath);
}
catch (SettingsException e)
{
    Console.Error.WriteLine($"{LogTag} {e.Message}");
    return 1;
}

using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop.Cancel();
};

await using var services = await DrillEggServices.CreateAsync(
    loaded,
    (swf, output) => ExtractCommand.Run(swf, output, Console.Out, Console.Error),
    Console.Out,
    TimeProvider.System,
    null,
    stop.Token).ConfigureAwait(false);

switch (command)
{
    case "coupons":
        return await GrantAsync().ConfigureAwait(false);
    case "prizes":
        PrintPrizes();
        return 0;
    case "history":
        await PrintHistoryAsync(arguments.Count > 1 ? arguments[1] : null).ConfigureAwait(false);
        return 0;
}

foreach (var warning in services.Warnings) Console.WriteLine($"{LogTag} warning: {warning}");
var interrupted = await services.Store.InterruptPendingAsync(stop.Token).ConfigureAwait(false);
if (interrupted > 0)
{
    Console.WriteLine($"{LogTag} warning: {interrupted} draw(s) were cut off when the server last stopped; they are marked \"interrupted\" in {loaded.DataFile}. Check them with: history");
}

var settings = loaded.Settings;
var port = settings.Port.ToString(CultureInfo.InvariantCulture);
var urls = settings.Listen switch
{
    ListenAddresses.Localhost => new[] { $"http://localhost:{port}" },
    ListenAddresses.Any => [$"http://*:{port}"],
    var address => [$"http://{(IPAddress.Parse(address).AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? $"[{address}]" : address)}:{port}"],
};
var server = new DrillEggServer(services, TimeProvider.System, Console.Out);
var app = server.Build(urls, Path.Combine(AppContext.BaseDirectory, "site"));

Console.WriteLine($"{LogTag} players: {settings.Players.Connector}, coupons: {settings.Coupons.Connector}, delivery: {settings.Delivery.Connector}");
Console.WriteLine(services.Art.Prepare() == tPoseidon.DrillEgg.Art.ArtState.Ready
    ? $"{LogTag} game art: {services.Art.ArtFolder}"
    : $"{LogTag} game art is not ready yet; open the page for the next step ({services.Art.SwfPath})");

try
{
    await app.StartAsync(stop.Token).ConfigureAwait(false);
}
catch (IOException e)
{
    Console.Error.WriteLine($"{LogTag} could not listen on port {port} ({e.Message.TrimEnd('.')}). Another program may be using it; change \"port\" in {Path.GetFileName(configPath)}.");
    return 1;
}

var local = $"http://localhost:{port}/";
Console.WriteLine($"{LogTag} the Drill Egg is running. Open {local} in your browser. Press Ctrl+C to stop.");
if (settings.OpenBrowser) OpenBrowser(local);
var retry = services.Retrier is { } retrier ? retrier.RunAsync(services.RetryEvery, stop.Token) : Task.CompletedTask;
try
{
    await app.WaitForShutdownAsync(stop.Token).ConfigureAwait(false);
    await retry.ConfigureAwait(false);
}
catch (OperationCanceledException) when (stop.IsCancellationRequested)
{
    Console.WriteLine($"{LogTag} stopped");
}
await app.DisposeAsync().ConfigureAwait(false);
return 0;

async Task<int> GrantAsync()
{
    if (arguments.Count != 3 || !int.TryParse(arguments[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count) || count == 0)
    {
        return Usage();
    }
    if (services.LocalCoupons is not { } coupons)
    {
        Console.Error.WriteLine($"{LogTag} coupons are only kept by the Drill Egg itself when coupons.connector is \"local\" (it is \"{loaded.Settings.Coupons.Connector}\").");
        return 1;
    }
    var left = await coupons.GrantAsync(arguments[1], count, stop.Token).ConfigureAwait(false);
    Console.WriteLine($"{LogTag} {arguments[1]} now has {left} coupon(s)");
    return 0;
}

void PrintPrizes()
{
    if (loaded.Settings.Coupons.Connector == ConnectorNames.Tposeidon)
    {
        Console.WriteLine($"{LogTag} with tposeidon coupons the prize pools come from the emulator's gacha_prize table.");
        return;
    }
    foreach (var pool in services.Catalog.Pools)
    {
        var total = pool.Prizes.Sum(p => (long)p.Weight);
        Console.WriteLine($"Dig site {pool.Area}, {(pool.Deep ? "Deep Drill" : "Normal Drill")} ({GachaRules.CouponsFor(pool.Deep)} coupon(s)):");
        foreach (var prize in pool.Prizes.OrderByDescending(p => p.Weight))
        {
            var chance = (double)prize.Weight / total;
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"  {chance,8:P2}  {services.Catalog.NameOf(prize.ItemIndex)} ({prize.ItemIndex}) x{prize.Count}"));
        }
    }
}

async Task PrintHistoryAsync(string? account)
{
    foreach (var draw in await services.Store.HistoryAsync(account, HistoryRows, stop.Token).ConfigureAwait(false))
    {
        Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{draw.DrawId,6}  {draw.DrawnAt.ToLocalTime():yyyy-MM-dd HH:mm}  {draw.Player.Account,-16}  site {draw.Area}{(draw.Deep ? " deep" : "     ")}  {draw.ItemName ?? "-"}  [{draw.Status}]{(draw.LastError is null ? string.Empty : "  " + draw.LastError)}"));
    }
}

static void OpenBrowser(string url)
{
    try
    {
        using var process = Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Win32Exception e)
    {
        Console.WriteLine($"{LogTag} could not open a browser ({e.Message}); open {url} yourself.");
    }
}

static int Usage()
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  drill-egg [--config <file>]                      start the Drill Egg (reads drill-egg.json in the current folder)");
    Console.Error.WriteLine("  drill-egg extract <myshop.swf> <art folder>      make the game art from your copy of myshop.swf");
    Console.Error.WriteLine("  drill-egg coupons <player> <count>               give a player coupons (a negative count takes them away)");
    Console.Error.WriteLine("  drill-egg prizes                                 show every prize and its chance");
    Console.Error.WriteLine("  drill-egg history [player]                       show the latest draws");
    return 2;
}
