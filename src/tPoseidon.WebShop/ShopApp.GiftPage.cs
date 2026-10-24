using System.Globalization;
using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.WebShop;

public sealed partial class ShopApp
{
    public const string GiftPage = "GiftItem.aspx";
    public const string FromField = "from";
    public const string ToField = "to";
    public const string MessageField = "message";

    public async Task<IResult> GiftItemPageAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        NoStore(context);
        var form = context.Request.HasFormContentType
            ? await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false)
            : null;
        if (form is not null && form.ContainsKey(ConfirmField))
        {
            if (FormSession(context, form) is not { } sender) return Page("Send a gift", Error("Your shop session has expired. Please open the item again."), StatusCodes.Status403Forbidden);
            var purchase = new CashPurchase(sender.AccountId, sender.World, IntField(form, ItemField), (short)IntField(form, LimitField), IntField(form, PriceField), CashSellStore.Game);
            var gift = new CashGift(purchase, ULongField(form, FromField), ULongField(form, ToField), form[MessageField].ToString().Trim());
            var outcome = _cash.Gift(gift, _cashPolicy);
            Console.WriteLine($"[webshop] gift goods {purchase.GoodsCode} from {sender.Username} to character {gift.ToCharacterId}: {outcome.Result}");
            return Page("Send a gift", ResultBox(outcome, "Your friend will find it in their gift box."));
        }

        var (session, refusal) = await EnterAsync(context).ConfigureAwait(false);
        if (session is null) return Results.Text(RefusalPage(refusal), "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status403Forbidden);
        var goods = _cash.FindGoods(IntField(form, ItemField));
        if (goods is null || !goods.OnSale) return Page("Send a gift", Error(CashShopRules.Describe(CashShopResult.UnknownGoods)));
        var from = ULongField(form, FromField);
        var friends = _cash.Friends(from, session.AccountId);
        if (friends.Count == 0) return Page("Send a gift", GoodsBox(goods) + Error("You can only send gifts to characters on your friend list, and it is empty."));

        var picker = new StringBuilder($"<div class=\"box\"><label>Send to<select name=\"{ToField}\">");
        foreach (var friend in friends)
        {
            picker.Append(CultureInfo.InvariantCulture, $"<option value=\"{friend.CharacterId}\">{Html(friend.Name)}</option>");
        }

        picker.Append(CultureInfo.InvariantCulture, $"</select></label><label>Message<input type=\"text\" name=\"{MessageField}\" maxlength=\"{CashShopRules.MaxGiftMessageLength}\"></label></div>");
        picker.Append(CultureInfo.InvariantCulture, $"<input type=\"hidden\" name=\"{FromField}\" value=\"{from}\">");
        var body = new StringBuilder()
            .Append(GoodsBox(goods))
            .Append(BalanceLine(session.AccountId))
            .Append(OrderForm(GiftPage, session, goods, picker.ToString(), buttonText: "Send gift"));
        return Page("Send a gift", body.ToString());
    }

    private static ulong ULongField(IFormCollection? form, string name) =>
        form is not null && ulong.TryParse(form[name].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
