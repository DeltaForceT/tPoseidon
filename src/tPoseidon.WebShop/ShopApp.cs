using System.Globalization;
using System.Net;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.WebShop;

public sealed partial class ShopApp
{
    public const string SessionCookie = "tpshop";
    public const string AccountField = "id";
    public const string WorldField = "world";

    private readonly IShopAccountStore _accounts;
    private readonly ShopSessions _sessions;
    private readonly ItemNames _names;
    private readonly TimeProvider _clock;
    private readonly ICashShopStore _cash;
    private readonly CashShopPolicy _cashPolicy;

    public ShopApp(IShopAccountStore accounts, ShopSessions sessions, ItemNames names, TimeProvider clock, ICashShopStore cash, CashShopPolicy cashPolicy)
    {
        _cash = cash ?? throw new ArgumentNullException(nameof(cash));
        _cashPolicy = cashPolicy ?? throw new ArgumentNullException(nameof(cashPolicy));
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _names = names ?? throw new ArgumentNullException(nameof(names));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

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
