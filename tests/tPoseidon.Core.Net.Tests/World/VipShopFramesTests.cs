using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class VipShopFramesTests
{
    private static readonly int[] TwoOffers = { 1, 0x101 };

    [Fact]
    public void TheListIsAShortCountAndIntIds()
    {
        Assert.Equal(new byte[] { 0x02, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00, 0x00 }, VipShopService.List(TwoOffers));
    }

    [Fact]
    public void AtMostSixtyFourOffersAreListed()
    {
        var payload = VipShopService.List(Enumerable.Range(1, 100).ToList());

        Assert.Equal(VipShopOffer.MaxListed, payload[0]);
        Assert.Equal(2 + VipShopOffer.MaxListed * 4, payload.Length);
    }

    [Fact]
    public void FunctionOneSeventyFiveOpensTheEggShop()
    {
        Assert.Equal(NpcScriptOperation.OpenVipShop, NpcScript.Classify(175).Operation);
    }
}
