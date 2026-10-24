using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Gacha;
using tPoseidon.DrillEgg.Art;
using tPoseidon.DrillEgg.Connectors;
using tPoseidon.DrillEgg.Prizes;

namespace tPoseidon.DrillEgg.Web;

internal sealed record DrawBody(int Area, bool Deep);

internal sealed class DrillEggServer
{
    public const string SessionCookie = "drillegg";
    public const string FormTokenHeader = "X-Shop-Form";
    public const string TokenQuery = "token";
    public const long MaxRequestBytes = 16 * 1024;
    public const string HtmlType = "text/html; charset=utf-8";
    public const string HostLogCategory = "Microsoft.Extensions.Hosting";
    public const string ContentSecurityPolicy = "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; style-src 'self'; script-src 'self'; connect-src 'self'; frame-ancestors 'self'; form-action 'self'; base-uri 'none'";

    private readonly DrillEggServices _services;
    private readonly PlayerSessions _sessions;
    private readonly SignInThrottle _throttle;
    private readonly TextWriter _log;
    private readonly FileExtensionContentTypeProvider _types = new();

    public DrillEggServer(DrillEggServices services, TimeProvider clock, TextWriter log)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(clock);
        _sessions = new PlayerSessions(clock);
        _throttle = new SignInThrottle(clock);
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public PlayerSessions Sessions => _sessions;

    public WebApplication Build(IReadOnlyList<string> urls, string siteFolder)
    {
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteFolder);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseUrls([.. urls]);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = MaxRequestBytes;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter(HostLogCategory, LogLevel.Critical);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            await next(context).ConfigureAwait(false);
        });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(Path.GetFullPath(siteFolder)) });
        app.MapGet("/", (Delegate)HomeAsync);
        app.MapGet("/sign-in", (Delegate)SignInPage);
        app.MapPost("/sign-in", (Delegate)SignInAsync);
        app.MapPost("/sign-out", (Delegate)SignOut);
        app.MapPost("/gacha/state", (Delegate)StateAsync);
        app.MapPost("/gacha/draw", DrawAsync);
        app.MapGet("/gacha/picture/{itemId:int}", Picture);
        app.MapGet("/gacha/art/{**path}", ArtFile);
        return app;
    }

    private async Task<IResult> HomeAsync(HttpContext context)
    {
        NoStore(context);
        if (_services.Art.Prepare() != ArtState.Ready) return Html(Pages.Setup(_services.Art), StatusCodes.Status200OK);
        var session = _sessions.Find(context.Request.Cookies[SessionCookie]);
        var players = _services.Players;
        var token = context.Request.Query[TokenQuery].ToString();
        if (players.Kind == SignInKind.Token && token.Length > 0)
        {
            var result = await TrySignInAsync(context, new SignInRequest(null, null, token)).ConfigureAwait(false);
            if (result is not null) return result;
            return Results.Redirect("/");
        }
        if (session is null && players.Kind == SignInKind.Automatic)
        {
            var result = await TrySignInAsync(context, new SignInRequest(null, null, null)).ConfigureAwait(false);
            if (result is not null) return result;
            session = _sessions.Find(context.Items[SessionCookie] as string);
        }
        if (session is null) return Results.Redirect("/sign-in");
        return Html(Pages.Game(session.FormToken, session.Player.Account, players.Kind != SignInKind.Automatic), StatusCodes.Status200OK);
    }

    private IResult SignInPage(HttpContext context)
    {
        NoStore(context);
        if (_services.Players.Kind == SignInKind.Automatic) return Results.Redirect("/");
        return Html(Pages.SignIn(_services.Players.Kind, null, null), StatusCodes.Status200OK);
    }

    private async Task<IResult> SignInAsync(HttpContext context)
    {
        NoStore(context);
        var kind = _services.Players.Kind;
        if (kind is SignInKind.Automatic or SignInKind.Token) return Results.Redirect("/");
        if (!context.Request.HasFormContentType) return Results.BadRequest();
        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var account = form["account"].ToString().Trim();
        var password = form["password"].ToString();
        var result = await TrySignInAsync(context, new SignInRequest(account, kind == SignInKind.Password ? password : null, null), account).ConfigureAwait(false);
        return result ?? Results.Redirect("/");
    }

    private IResult SignOut(HttpContext context)
    {
        _sessions.Close(context.Request.Cookies[SessionCookie]);
        context.Response.Cookies.Delete(SessionCookie);
        return Results.Redirect(_services.Players.Kind == SignInKind.Automatic ? "/" : "/sign-in");
    }

    private async Task<IResult> StateAsync(HttpContext context)
    {
        NoStore(context);
        if (Authorized(context) is not { } session) return Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            var coupons = await _services.Engine.CouponsAsync(session.Player, context.RequestAborted).ConfigureAwait(false);
            return Results.Json(new { account = session.Player.Account, coupons, world = 0 });
        }
        catch (ConnectorException e)
        {
            _log.WriteLine($"[drill-egg] the coupon count for {session.Player.Account} failed: {e.Message}");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<IResult> DrawAsync(HttpContext context, DrawBody body)
    {
        NoStore(context);
        if (Authorized(context) is not { } session) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (body is null || !GachaRules.IsArea(body.Area)) return Results.BadRequest();
        var remote = (context.Connection.RemoteIpAddress ?? IPAddress.None).ToString();
        try
        {
            var outcome = await _services.Engine.DrawAsync(session.Player, (short)body.Area, body.Deep, remote, context.RequestAborted).ConfigureAwait(false);
            var picture = outcome.Result.Prize is { } prize ? PrizePictures.UrlOf(prize.ItemIndex) : null;
            return Results.Json(GachaReply.From(outcome.Result, outcome.ItemName, picture));
        }
        catch (ConnectorException e)
        {
            _log.WriteLine($"[drill-egg] the draw for {session.Player.Account} failed and nothing was spent: {e.Message}");
            return Results.Json(GachaReply.Failure());
        }
    }

    private IResult Picture(HttpContext context, int itemId)
    {
        context.Response.Headers.CacheControl = "no-cache";
        if (_services.Pictures.Find(itemId) is { } path) return Results.File(path, PrizePictureNames.ContentTypeOf(path));
        return Results.Text(_services.Pictures.Placeholder(itemId), PrizePictures.PlaceholderType, Encoding.UTF8);
    }

    private IResult ArtFile(string path)
    {
        var root = _services.Art.ArtFolder;
        var full = Path.GetFullPath(Path.Combine(root, path));
        var inside = full.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (!inside || !File.Exists(full)) return Results.NotFound();
        return Results.File(full, _types.TryGetContentType(full, out var type) ? type : "application/octet-stream");
    }

    private async Task<IResult?> TrySignInAsync(HttpContext context, SignInRequest request, string? typed = null)
    {
        var remote = context.Connection.RemoteIpAddress ?? IPAddress.None;
        var players = _services.Players;
        if (_throttle.IsBlocked(remote))
        {
            return Html(Pages.SignIn(players.Kind, "Too many tries. Please wait ten minutes and try again.", typed), StatusCodes.Status429TooManyRequests);
        }
        GachaPlayer? player;
        try
        {
            player = await players.SignInAsync(request, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ConnectorException e)
        {
            _log.WriteLine($"[drill-egg] sign-in failed: {e.Message}");
            return Html(Pages.Problem("Sign-in is not available", "The game server's sign-in could not be reached. Please try again later."), StatusCodes.Status503ServiceUnavailable);
        }
        if (player is null)
        {
            _throttle.Failed(remote);
            var message = players.Kind switch
            {
                SignInKind.Name => $"Please use {PlayerNames.Rule}.",
                SignInKind.Token => "This link is not valid any more. Please ask the game server for a new one.",
                _ => "The account or password is not right.",
            };
            return Html(Pages.SignIn(players.Kind, message, typed), StatusCodes.Status401Unauthorized);
        }
        _throttle.Succeeded(remote);
        _sessions.Close(context.Request.Cookies[SessionCookie]);
        var session = _sessions.Open(player);
        context.Items[SessionCookie] = session.Token;
        context.Response.Cookies.Append(SessionCookie, session.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps,
            Path = "/",
            IsEssential = true,
            MaxAge = PlayerSessions.Lifetime,
        });
        _log.WriteLine(string.Create(CultureInfo.InvariantCulture, $"[drill-egg] {player.Account} signed in from {remote}"));
        return null;
    }

    private PlayerSession? Authorized(HttpContext context) =>
        _sessions.Authorize(context.Request.Cookies[SessionCookie], context.Request.Headers[FormTokenHeader].ToString());

    private static IResult Html(string html, int status) => Results.Text(html, HtmlType, Encoding.UTF8, status);

    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";
}
