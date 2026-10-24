using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using tPoseidon.Core.Data;

namespace tPoseidon.WebShop;

public sealed partial class ShopApp
{
    public const string BuyPage = "BuyItem.aspx";
    public const string ItemField = "item";
    public const string LimitField = "limit";
    public const string PriceField = "price";
    public const string ConfirmField = "confirm";
    public const string FormTokenField = "form";

    private const string PageStyle =
        "body{background:#10131a;color:#e8e2d0;font:13px Tahoma,Arial,sans-serif;margin:12px}" +
        "h1{font-size:15px;margin:0 0 8px}.box{border:1px solid #4a4434;padding:8px;margin-bottom:8px}" +
        ".price{color:#f2c94c}.note{color:#9aa0a6}.error{color:#ff8a80}button{margin-top:8px;padding:4px 14px}" +
        "label{display:block;margin:3px 0}select,input[type=text]{width:100%;box-sizing:border-box}";

    public async Task<IResult> BuyItemPageAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NoStore(context);
        var form = context.Request.HasFormContentType
            ? await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false)
            : null;
        if (form is not null && form.ContainsKey(ConfirmField))
        {
            if (FormSession(context, form) is not { } buyer) return Page("Item shop", Error("Your shop session has expired. Please open the item again."), StatusCodes.Status403Forbidden);
            var purchase = new CashPurchase(buyer.AccountId, buyer.World, IntField(form, ItemField), (short)IntField(form, LimitField), IntField(form, PriceField), CashSellStore.Game);
            var outcome = _cash.Buy(purchase, _cashPolicy);
            Console.WriteLine($"[webshop] buy goods {purchase.GoodsCode} limit {purchase.LimitCode} by {buyer.Username}: {outcome.Result}");
            return Page("Item shop", ResultBox(outcome, "Find it under My Item in the item shop window."));
        }

        var (session, refusal) = await EnterAsync(context).ConfigureAwait(false);
        if (session is null) return Results.Text(RefusalPage(refusal), "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        var goods = _cash.FindGoods(IntField(form, ItemField));
        if (goods is null || !goods.OnSale) return Page("Item shop", Error(CashShopRules.Describe(CashShopResult.UnknownGoods)));
        var body = new StringBuilder()
            .Append(GoodsBox(goods))
            .Append(BalanceLine(session.AccountId))
            .Append(OrderForm(BuyPage, session, goods, extraFields: string.Empty, buttonText: "Buy"));
        return Page(goods.Name, body.ToString());
    }

    private ShopSession? FormSession(HttpContext context, IFormCollection form)
    {
        var session = _sessions.Find(context.Request.Cookies[SessionCookie], RemoteOf(context), _clock.GetUtcNow());
        if (session is null || !string.Equals(form[FormTokenField].ToString(), session.FormToken, StringComparison.Ordinal)) return null;
        return ShopGate.Check(_accounts.Find(session.AccountId), session.RemoteIp) == ShopRefusal.None ? session : null;
    }

    private string GoodsBox(ShopGoods goods)
    {
        var items = new StringBuilder();
        foreach (var item in goods.Items)
        {
            var name = _names.Of(item.ItemIndex) ?? $"Item {item.ItemIndex}";
            items.Append(CultureInfo.InvariantCulture, $"<div>{Html(name)}{(item.ItemCount > 1 ? $" x{item.ItemCount}" : string.Empty)}</div>");
        }

        var description = goods.Description.Length > 0 ? $"<div class=\"note\">{Html(goods.Description)}</div>" : string.Empty;
        return $"<div class=\"box\"><h1>{Html(goods.Name)}</h1>{items}{description}</div>";
    }

    private string BalanceLine(uint accountId) => _cashPolicy.Mode switch
    {
        CashMode.Free => "<div class=\"note\">Everything in this shop is free on this server.</div>",
        CashMode.Balance => $"<div>Your cash: <span class=\"price\">{_cash.Balance(accountId, _cashPolicy.StartingBalance).ToString("N0", CultureInfo.InvariantCulture)}</span></div>",
        _ => $"<div class=\"note\">{CashShopRules.Describe(CashShopResult.ShopClosed)}</div>",
    };

    private string OrderForm(string page, ShopSession session, ShopGoods goods, string extraFields, string buttonText)
    {
        if (goods.Options.Count == 0) return Error(CashShopRules.Describe(CashShopResult.UnknownGoods));
        var options = new StringBuilder();
        var first = true;
        foreach (var option in goods.Options)
        {
            var price = _cashPolicy.PriceOf(option);
            var duration = option.LimitFlag == 1 && option.UseHours > 0 ? DurationText(option.UseHours) : "Permanent";
            var priceText = price == 0 ? "Free" : price.ToString("N0", CultureInfo.InvariantCulture);
            options.Append(CultureInfo.InvariantCulture,
                $"<label><input type=\"radio\" name=\"{LimitField}\" value=\"{option.LimitCode}\" data-price=\"{price}\"{(first ? " checked" : string.Empty)} onclick=\"this.form.{PriceField}.value=this.dataset.price\"> {duration} <span class=\"price\">{priceText}</span></label>");
            first = false;
        }

        var button = _cashPolicy.Mode == CashMode.Disabled ? string.Empty : $"<button type=\"submit\">{buttonText}</button>";
        return $"<form method=\"post\" action=\"{page}\"><div class=\"box\">{options}</div>{extraFields}" +
            $"<input type=\"hidden\" name=\"{ItemField}\" value=\"{goods.GoodsCode}\">" +
            $"<input type=\"hidden\" name=\"{PriceField}\" value=\"{_cashPolicy.PriceOf(goods.Options[0])}\">" +
            $"<input type=\"hidden\" name=\"{FormTokenField}\" value=\"{session.FormToken}\">" +
            $"<input type=\"hidden\" name=\"{ConfirmField}\" value=\"1\">{button}</form>";
    }

    private static string DurationText(short hours) =>
        hours % 24 == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours / 24} day{(hours == 24 ? string.Empty : "s")}")
            : string.Create(CultureInfo.InvariantCulture, $"{hours} hour{(hours == 1 ? string.Empty : "s")}");

    private string ResultBox(CashShopOutcome outcome, string successNote)
    {
        if (outcome.Result != CashShopResult.Ok) return Error(CashShopRules.Describe(outcome.Result));
        var balance = _cashPolicy.Mode == CashMode.Balance
            ? $"<div>Cash left: <span class=\"price\">{outcome.Balance.ToString("N0", CultureInfo.InvariantCulture)}</span></div>"
            : string.Empty;
        return $"<div class=\"box\"><h1>Done</h1><div>{Html(successNote)}</div>{balance}</div>";
    }

    private static string Error(string text) => $"<div class=\"box error\">{Html(text)}</div>";

    private static int IntField(IFormCollection? form, string name) =>
        form is not null && int.TryParse(form[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static string Html(string text) => HtmlEncoder.Default.Encode(text);

    private static IResult Page(string title, string body, int status = StatusCodes.Status200OK) =>
        Results.Text(
            $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{Html(title)}</title><style>{PageStyle}</style></head><body>{body}</body></html>",
            "text/html; charset=utf-8",
            Encoding.UTF8,
            status);
}
