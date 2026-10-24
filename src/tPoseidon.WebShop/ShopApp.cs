using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Gacha;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.WebShop;

public sealed record GachaDrawBody(int Area, bool Deep);

public sealed partial class ShopApp
{
    public const string SessionCookie = "tpshop";
    public const string FormTokenHeader = "X-Shop-Form";
    public const string FormTokenMeta = "shop-form";
    public const string GachaPage = "gacha/index.html";
    public const string AccountField = "id";
    public const string WorldField = "world";

    private readonly IShopAccountStore _accounts;
    private readonly IGachaStore _gacha;
    private readonly ShopSessions _sessions;
    private readonly ItemNames _names;
    private readonly string _pagesDirectory;
    private readonly TimeProvider _clock;
    private readonly ICashShopStore _cash;
    private readonly CashShopPolicy _cashPolicy;

    public ShopApp(IShopAccountStore accounts, IGachaStore gacha, ShopSessions sessions, ItemNames names, string pagesDirectory, TimeProvider clock, ICashShopStore cash, CashShopPolicy cashPolicy)
    {
        _cash = cash ?? throw new ArgumentNullException(nameof(cash));
        _cashPolicy = cashPolicy ?? throw new ArgumentNullException(nameof(cashPolicy));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _gacha = gacha ?? throw new ArgumentNullException(nameof(gacha));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _pagesDirectory = pagesDirectory ?? throw new ArgumentNullException(nameof(pagesDirectory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<IResult> GachaPageAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (session, refusal) = await EnterAsync(context).ConfigureAwait(false);
        if (session is null)
        {
            Console.WriteLine($"[webshop] gacha page refused for {RemoteOf(context)}: {refusal}");
            return Results.Text(RefusalPage(refusal), "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        }
        var path = Path.Combine(_pagesDirectory, GachaPage);
        if (!File.Exists(path)) return Results.Text("The gacha page is not installed.", "text/plain", Encoding.UTF8, StatusCodes.Status503ServiceUnavailable);
        var html = await File.ReadAllTextAsync(path, context.RequestAborted).ConfigureAwait(false);
        var meta = $"<meta name=\"{FormTokenMeta}\" content=\"{session.FormToken}\">";
        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        html = headEnd < 0 ? meta + html : html.Insert(headEnd, meta);
        NoStore(context);
        return Results.Text(html, "text/html; charset=utf-8", Encoding.UTF8);
    }

    public IResult GachaState(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Authorized(context) is not { } session) return Results.StatusCode(StatusCodes.Status403Forbidden);
        NoStore(context);
        return Results.Json(new { account = session.Username, coupons = _gacha.CouponCount(session.AccountId), world = session.World });
    }

    public IResult GachaDraw(HttpContext context, GachaDrawBody body)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(body);
        if (Authorized(context) is not { } session) return Results.StatusCode(StatusCodes.Status403Forbidden);
        if (!GachaRules.IsArea(body.Area)) return Results.BadRequest();
        var result = _gacha.Draw(
            new GachaDrawRequest(session.AccountId, (short)body.Area, body.Deep, session.RemoteIp.ToString()),
            GachaPicker.Pick);
        NoStore(context);
        Console.WriteLine($"[webshop] gacha draw {session.Username} area {body.Area}{(body.Deep ? " deep" : string.Empty)}: {result.Outcome} {result.Prize?.ItemIndex.ToString(CultureInfo.InvariantCulture) ?? "-"}, {result.CouponsLeft} coupon(s) left");
        return Results.Json(GachaReply.From(result, result.Prize is { } prize ? _names.Of(prize.ItemIndex) : null));
    }

    public static IResult Close() => Results.Text(string.Empty, "text/html");

    private async Task<(ShopSession? Session, ShopRefusal Refusal)> EnterAsync(HttpContext context)
    {
        var remote = RemoteOf(context);
        var now = _clock.GetUtcNow();
        if (context.Request.HasFormContentType)
        {
            var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
            if (form.TryGetValue(AccountField, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                var account = _accounts.Find(id.ToString().Trim());
                var refusal = ShopGate.Check(account, remote);
                if (refusal != ShopRefusal.None) return (null, refusal);
                var world = int.TryParse(form[WorldField], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) ? w : 0;
                return (Start(context, account!, world, remote, now), ShopRefusal.None);
            }
        }
        if (_sessions.Find(context.Request.Cookies[SessionCookie], remote, now) is { } existing) return (existing, ShopRefusal.None);
        var inGame = _accounts.InGameAt(remote);
        if (inGame.Count != 1) return (null, inGame.Count == 0 ? ShopRefusal.NotInGame : ShopRefusal.OtherAddress);
        return (Start(context, inGame[0], 0, remote, now), ShopRefusal.None);
    }

    private ShopSession Start(HttpContext context, ShopAccount account, int world, IPAddress remote, DateTimeOffset now)
    {
        _sessions.Close(context.Request.Cookies[SessionCookie]);
        var session = _sessions.Open(account.AccountId, account.Username, world, remote, now);
        context.Response.Cookies.Append(SessionCookie, session.Token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            IsEssential = true,
        });
        Console.WriteLine($"[webshop] session opened for {account.Username} from {remote}");
        return session;
    }

    private ShopSession? Authorized(HttpContext context)
    {
        var session = _sessions.Find(context.Request.Cookies[SessionCookie], RemoteOf(context), _clock.GetUtcNow());
        if (session is null) return null;
        var form = context.Request.Headers[FormTokenHeader].ToString();
        if (!string.Equals(form, session.FormToken, StringComparison.Ordinal)) return null;
        var account = _accounts.Find(session.AccountId);
        return ShopGate.Check(account, session.RemoteIp) == ShopRefusal.None ? session : null;
    }

    private static IPAddress RemoteOf(HttpContext context) =>
        ShopGate.Normalize(context.Connection.RemoteIpAddress ?? IPAddress.None);

    private static void NoStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";

    private static string RefusalPage(ShopRefusal refusal) => refusal switch
    {
        ShopRefusal.NotInGame => "<html><body style=\"background:#000;color:#fff\">Please enter the game world first.</body></html>",
        ShopRefusal.OtherAddress => "<html><body style=\"background:#000;color:#fff\">This page must be opened from your game client.</body></html>",
        _ => "<html><body style=\"background:#000;color:#fff\">Account not found.</body></html>",
    };
}
