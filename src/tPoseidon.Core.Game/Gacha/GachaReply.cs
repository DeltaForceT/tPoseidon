using System.Text.Json.Serialization;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Gacha;

public sealed record GachaReply(
    string Result,
    int? Item,
    string? Name,
    short? Count,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Coupons,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Picture)
{
    public const string Won = "won";
    public const string NotEnoughCoupons = "coupons";
    public const string Empty = "empty";
    public const string Error = "error";

    public static GachaReply From(GachaDrawResult result, string? name, string? picture = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new GachaReply(
            result.Outcome switch
            {
                GachaDrawOutcome.Won => Won,
                GachaDrawOutcome.NotEnoughCoupons => NotEnoughCoupons,
                _ => Empty,
            },
            result.Prize?.ItemIndex,
            result.Prize is null ? null : name,
            result.Prize?.Count,
            result.CouponsLeft,
            result.Prize is null ? null : picture);
    }

    public static GachaReply Failure() => new(Error, null, null, null, null, null);
}
