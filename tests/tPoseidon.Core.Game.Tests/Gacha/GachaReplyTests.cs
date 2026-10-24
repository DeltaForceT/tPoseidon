using System.Text.Json;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Gacha;

namespace tPoseidon.Core.Game.Tests.Gacha;

public sealed class GachaReplyTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Win_CarriesThePrizeAndTheCouponsLeft()
    {
        var prize = new GachaPrize(5001, 1, 3, 0, 0, 10);
        var reply = GachaReply.From(new GachaDrawResult(GachaDrawOutcome.Won, prize, 77, 4), "Egg");

        Assert.Equal("""{"result":"won","item":5001,"name":"Egg","count":3,"coupons":4}""", JsonSerializer.Serialize(reply, Web));
    }

    [Fact]
    public void Win_WithAPicture_AddsItsAddress()
    {
        var prize = new GachaPrize(7, 1, 1, 0, 0, 1);
        var reply = GachaReply.From(new GachaDrawResult(GachaDrawOutcome.Won, prize, 0, 0), "Seven", "/gacha/picture/7");

        Assert.Equal("/gacha/picture/7", reply.Picture);
        Assert.Contains("\"picture\":\"/gacha/picture/7\"", JsonSerializer.Serialize(reply, Web), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(GachaDrawOutcome.NotEnoughCoupons, "coupons")]
    [InlineData(GachaDrawOutcome.EmptyPool, "empty")]
    public void Refusals_CarryNoPrize(GachaDrawOutcome outcome, string expected)
    {
        var reply = GachaReply.From(new GachaDrawResult(outcome, null, 0, 1), "ignored", "/ignored");

        Assert.Equal($$"""{"result":"{{expected}}","item":null,"name":null,"count":null,"coupons":1}""", JsonSerializer.Serialize(reply, Web));
    }

    [Fact]
    public void Failure_HasNoCouponCount()
    {
        Assert.Equal("""{"result":"error","item":null,"name":null,"count":null}""", JsonSerializer.Serialize(GachaReply.Failure(), Web));
    }
}
