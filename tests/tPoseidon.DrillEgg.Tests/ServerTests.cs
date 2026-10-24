using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using tPoseidon.DrillEgg.Art;
using tPoseidon.DrillEgg.Prizes;
using tPoseidon.DrillEgg.Web;

namespace tPoseidon.DrillEgg.Tests;

public sealed class ArtSetupTests
{
    private static int WriteLibrary(string swf, string output)
    {
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, ArtSetup.LibraryFile), "{}");
        return 0;
    }

    [Fact]
    public void WithoutTheFile_TheArtIsMissing()
    {
        using var folder = new TempFolder();
        var art = new ArtSetup(folder.File("art"), folder.File("myshop.swf"), WriteLibrary, TextWriter.Null);

        Assert.Equal(ArtState.Missing, art.Prepare());
        Assert.False(Directory.Exists(folder.File("art")));
    }

    [Fact]
    public void WithTheFile_TheArtIsMadeOnce()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.File("myshop.swf"), "movie");
        var runs = 0;
        var art = new ArtSetup(folder.File("art"), folder.File("myshop.swf"), (swf, output) =>
        {
            runs++;
            Assert.Equal(folder.File("myshop.swf"), swf);
            return WriteLibrary(swf, output);
        }, TextWriter.Null);

        Assert.Equal(ArtState.Ready, art.Prepare());
        Assert.Equal(ArtState.Ready, art.Prepare());
        Assert.Equal(1, runs);
        Assert.True(File.Exists(Path.Combine(folder.File("art"), ArtSetup.LibraryFile)));
        Assert.False(Directory.Exists(folder.File("art") + ArtSetup.PartialSuffix));
    }

    [Fact]
    public void ABadFile_LeavesNothingHalfMade()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.File("myshop.swf"), "not a movie");
        var art = new ArtSetup(folder.File("art"), folder.File("myshop.swf"), (_, output) =>
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "half.png"), "x");
            return 1;
        }, TextWriter.Null);

        Assert.Equal(ArtState.Failed, art.Prepare());
        Assert.Contains("could not be read", art.Problem, StringComparison.Ordinal);
        Assert.False(Directory.Exists(folder.File("art")));
        Assert.False(Directory.Exists(folder.File("art") + ArtSetup.PartialSuffix));
    }

    [Fact]
    public void AnUnrelatedArtFolder_IsNeverOverwritten()
    {
        using var folder = new TempFolder();
        File.WriteAllText(folder.File("myshop.swf"), "movie");
        Directory.CreateDirectory(folder.File("art"));
        File.WriteAllText(Path.Combine(folder.File("art"), "mine.txt"), "keep");
        var art = new ArtSetup(folder.File("art"), folder.File("myshop.swf"), WriteLibrary, TextWriter.Null);

        Assert.Equal(ArtState.Failed, art.Prepare());
        Assert.True(File.Exists(Path.Combine(folder.File("art"), "mine.txt")));
    }
}

public sealed class PrizePictureTests
{
    [Fact]
    public void Pictures_AreFoundByNameIdOrSetting()
    {
        using var folder = new TempFolder();
        var catalog = PrizeCatalog.From(Fixtures.Settings(folder.Path, """
            "prizes": [
              { "id": 1, "name": "Small Healing Potion", "weight": 1 },
              { "id": 2, "name": "Crystal Egg", "weight": 1 },
              { "id": 3, "name": "Gem", "weight": 1, "picture": "shiny.gif" },
              { "id": 4, "name": "Map", "weight": 1 }
            ]
            """).Settings);
        var pictures = Path.Combine(folder.Path, "pictures");
        Directory.CreateDirectory(pictures);
        File.WriteAllText(Path.Combine(pictures, "1.png"), "a");
        File.WriteAllText(Path.Combine(pictures, "Crystal Egg.webp"), "b");
        File.WriteAllText(Path.Combine(pictures, "shiny.gif"), "c");
        File.WriteAllText(Path.Combine(pictures, "4.svg"), "<svg/>");
        var finder = new PrizePictures(pictures, catalog);

        Assert.Equal(Path.Combine(pictures, "1.png"), finder.Find(1));
        Assert.Equal(Path.Combine(pictures, "Crystal Egg.webp"), finder.Find(2));
        Assert.Equal(Path.Combine(pictures, "shiny.gif"), finder.Find(3));
        Assert.Null(finder.Find(4));
        Assert.Equal("/gacha/picture/4", PrizePictures.UrlOf(4));
    }

    [Fact]
    public void ThePlaceholder_IsAnSvgCardWithTheEscapedName()
    {
        var svg = PrizePictures.PlaceholderSvg("Tom & Jerry's <Golden> Legendary Drill Egg of the Deep");

        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("Tom &amp; Jerry", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<Golden>", svg, StringComparison.Ordinal);
        Assert.Equal(3, Regex.Count(svg, "<text "));
        Assert.Contains("…", svg, StringComparison.Ordinal);
    }
}

public sealed class ServerTests
{
    private const string Site = "site";

    private sealed class Running : IAsyncDisposable
    {
        public required TempFolder Folder { get; init; }

        public required WebApplication App { get; init; }

        public required DrillEggServices Services { get; init; }

        public required HttpClient Client { get; init; }

        public required CookieContainer Cookies { get; init; }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            await Services.DisposeAsync();
            Folder.Dispose();
        }
    }

    private static async Task<Running> StartAsync(string settings, bool art = true)
    {
        var folder = new TempFolder();
        if (art)
        {
            Directory.CreateDirectory(folder.File("art"));
            File.WriteAllText(Path.Combine(folder.File("art"), ArtSetup.LibraryFile), """{"root":{}}""");
        }
        var site = folder.File(Site);
        Directory.CreateDirectory(Path.Combine(site, "gacha"));
        File.WriteAllText(Path.Combine(site, "site.css"), "body{}");
        File.WriteAllText(Path.Combine(site, "gacha", "drill.js"), "var x;");
        var services = await DrillEggServices.CreateAsync(Fixtures.Settings(folder.Path, settings), (_, _) => 1, TextWriter.Null, TimeProvider.System, null, CancellationToken.None);
        var app = new DrillEggServer(services, TimeProvider.System, TextWriter.Null).Build(["http://127.0.0.1:0"], site);
        await app.StartAsync();
        var cookies = new CookieContainer();
        var client = new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.First()) };
        return new Running { Folder = folder, App = app, Services = services, Client = client, Cookies = cookies };
    }

    private static string FormToken(string html) => Regex.Match(html, "name=\"shop-form\" content=\"([0-9a-f]+)\"").Groups[1].Value;

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, string form, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add(DrillEggServer.FormTokenHeader, form);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<string> GameAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task WithoutArt_TheSetupPageSaysWhereToPutTheFile()
    {
        await using var server = await StartAsync(Fixtures.Prizes, art: false);

        var html = await GameAsync(server.Client);

        Assert.Contains("myshop.swf", html, StringComparison.Ordinal);
        Assert.Contains(WebUtilityEncode(server.Folder.File("myshop.swf")), html, StringComparison.Ordinal);
        Assert.Contains("Check again", html, StringComparison.Ordinal);
        Assert.DoesNotContain("shop-form", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FreePlay_SignsTheSinglePlayerInAndDraws()
    {
        await using var server = await StartAsync(Fixtures.Prizes);
        var html = await GameAsync(server.Client);
        var form = FormToken(html);

        var state = await PostAsync(server.Client, "/gacha/state", form);
        var draw = await PostAsync(server.Client, "/gacha/draw", form, new { area = 63, deep = true });

        Assert.Contains("/gacha/drill.js", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sign out", html, StringComparison.Ordinal);
        Assert.Equal("Player", state.GetProperty("account").GetString());
        Assert.Equal(99, state.GetProperty("coupons").GetInt32());
        Assert.Equal("won", draw.GetProperty("result").GetString());
        var item = draw.GetProperty("item").GetInt32();
        Assert.Equal($"/gacha/picture/{item}", draw.GetProperty("picture").GetString());
        Assert.Equal(server.Services.Catalog.NameOf(item), draw.GetProperty("name").GetString());
    }

    [Fact]
    public async Task TheGameCalls_NeedTheCookieAndTheFormToken()
    {
        await using var server = await StartAsync(Fixtures.Prizes);
        await GameAsync(server.Client);

        using var noToken = await server.Client.PostAsync("/gacha/state", null);
        using var stranger = new HttpClient { BaseAddress = server.Client.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/gacha/state");
        request.Headers.Add(DrillEggServer.FormTokenHeader, "00");
        using var noCookie = await stranger.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, noCookie.StatusCode);
    }

    [Fact]
    public async Task ADigSiteOutsideTheGame_IsRefused()
    {
        await using var server = await StartAsync(Fixtures.Prizes);
        var form = FormToken(await GameAsync(server.Client));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/gacha/draw") { Content = JsonContent.Create(new { area = 65, deep = false }) };
        request.Headers.Add(DrillEggServer.FormTokenHeader, form);

        using var response = await server.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CouponMode_RunsOutOfCoupons()
    {
        await using var server = await StartAsync("""
            "players": { "connector": "local", "mode": "name" },
            "coupons": { "connector": "local", "startingCoupons": 2 },
            """ + Fixtures.Prizes);
        using (var signIn = await server.Client.GetAsync("/"))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
            Assert.Equal("/sign-in", signIn.Headers.Location!.OriginalString);
        }
        using (var bad = await server.Client.PostAsync("/sign-in", new FormUrlEncodedContent([new("account", "<script>")])))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        }
        using (var good = await server.Client.PostAsync("/sign-in", new FormUrlEncodedContent([new("account", "Alice")])))
        {
            Assert.Equal(HttpStatusCode.Redirect, good.StatusCode);
        }
        var html = await GameAsync(server.Client);
        var form = FormToken(html);

        var deep = await PostAsync(server.Client, "/gacha/draw", form, new { area = 61, deep = true });
        var empty = await PostAsync(server.Client, "/gacha/draw", form, new { area = 61, deep = false });
        var state = await PostAsync(server.Client, "/gacha/state", form);

        Assert.Contains("Sign out", html, StringComparison.Ordinal);
        Assert.Equal("won", deep.GetProperty("result").GetString());
        Assert.Equal(0, deep.GetProperty("coupons").GetInt32());
        Assert.Equal("coupons", empty.GetProperty("result").GetString());
        Assert.Equal("Alice", state.GetProperty("account").GetString());
        Assert.Equal(0, state.GetProperty("coupons").GetInt32());
    }

    [Fact]
    public async Task TooManyWrongSignIns_AreSlowedDown()
    {
        await using var server = await StartAsync("""
            "players": { "connector": "local", "mode": "name" },
            """ + Fixtures.Prizes);

        for (var i = 0; i < SignInThrottle.MaxFailures; i++)
        {
            using var wrong = await server.Client.PostAsync("/sign-in", new FormUrlEncodedContent([new("account", "")]));
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        using var blocked = await server.Client.PostAsync("/sign-in", new FormUrlEncodedContent([new("account", "Alice")]));

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task PicturesArtAndSiteFiles_AreServed_AndNothingOutsideTheArt()
    {
        await using var server = await StartAsync(Fixtures.Prizes);

        using var placeholder = await server.Client.GetAsync("/gacha/picture/2");
        using var library = await server.Client.GetAsync("/gacha/art/library.json");
        using var script = await server.Client.GetAsync("/gacha/drill.js");
        using var escape = await server.Client.GetAsync("/gacha/art/..%2Fdrill-egg.db");

        Assert.Equal("image/svg+xml", placeholder.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Crystal Egg", await placeholder.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("application/json", library.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, escape.StatusCode);
        Assert.Equal("nosniff", placeholder.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task AFailingGameServer_SpendsNothingAndShowsTheErrorScreen()
    {
        await using var server = await StartAsync("""
            "coupons": { "connector": "http", "countUrl": "http://127.0.0.1:9/count", "spendUrl": "http://127.0.0.1:9/spend", "timeoutSeconds": 2 },
            """ + Fixtures.Prizes);
        var form = FormToken(await GameAsync(server.Client));

        using var request = new HttpRequestMessage(HttpMethod.Post, "/gacha/state");
        request.Headers.Add(DrillEggServer.FormTokenHeader, form);
        using var state = await server.Client.SendAsync(request);
        var draw = await PostAsync(server.Client, "/gacha/draw", form, new { area = 62, deep = false });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, state.StatusCode);
        Assert.Equal("error", draw.GetProperty("result").GetString());
        Assert.False(draw.TryGetProperty("coupons", out _));
        Assert.Empty(await server.Services.Store.HistoryAsync(null, 10, CancellationToken.None));
    }

    private static string WebUtilityEncode(string text) => WebUtility.HtmlEncode(text);
}
