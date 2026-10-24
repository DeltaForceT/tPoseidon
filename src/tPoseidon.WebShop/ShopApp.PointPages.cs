using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;
using tPoseidon.Core.Data;

namespace tPoseidon.WebShop;

public sealed partial class ShopApp
{
    public const string FreePointText = "Free";
    public const string ClosedPointText = "0";

    public async Task<IResult> MyPointAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NoStore(context);
        var form = context.Request.HasFormContentType
            ? await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false)
            : null;
        var name = form?[AccountField].ToString().Trim();
        var account = string.IsNullOrEmpty(name) ? null : _accounts.Find(name);
        if (ShopGate.Check(account, RemoteOf(context)) != ShopRefusal.None)
        {
            return Results.Text(ClosedPointText, "text/plain; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        }

        var text = _cashPolicy.Mode switch
        {
            CashMode.Free => FreePointText,
            CashMode.Balance => _cash.Balance(account!.AccountId, _cashPolicy.StartingBalance).ToString(CultureInfo.InvariantCulture),
            _ => ClosedPointText,
        };
        return Results.Text(text, "text/plain; charset=utf-8", Encoding.UTF8);
    }

    public async Task<IResult> ChargePointPageAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NoStore(context);
        var (session, refusal) = await EnterAsync(context).ConfigureAwait(false);
        if (session is null) return Results.Text(RefusalPage(refusal), "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        var body = _cashPolicy.Mode switch
        {
            CashMode.Free => "<div class=\"box\"><h1>Cash</h1><div>Everything in this shop is free on this server, so there is nothing to charge.</div></div>",
            CashMode.Balance => "<div class=\"box\"><h1>Cash</h1>" + BalanceLine(session.AccountId) +
                "<div class=\"note\">Cash on this server is handed out by its host. Ask them if you need more.</div></div>",
            _ => "<div class=\"box\"><h1>Cash</h1>" + Error(CashShopRules.Describe(CashShopResult.ShopClosed)) + "</div>",
        };
        return Page("Cash", body);
    }
}
