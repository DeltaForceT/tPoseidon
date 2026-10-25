using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Wedding;

public sealed class CoupleRulesTests
{
    private static readonly CouplePointRange Any = new(0, 100, 0, 100, 0, 100);

    private static CoupleTables Tables() => new(
        [
            new MarriageExchangeEntry(39000, "set", 1, 39000, 1, Any, 0, 256),
            new MarriageExchangeEntry(39500, "ring", 1, 0, 0, new CouplePointRange(0, 100, 0, 55, 0, 55), 30000, 128),
            new MarriageExchangeEntry(39509, "ring", 1, 0, 0, new CouplePointRange(0, 100, 100, 100, 100, 100), 70000, 128),
        ],
        [
            new MarriageTitleEntry(1, "a", new CouplePointRange(86, 100, 100, 100, 100, 100)),
            new MarriageTitleEntry(11, "b", new CouplePointRange(0, 85, 0, 100, 0, 100)),
            new MarriageTitleEntry(12, "c", new CouplePointRange(86, 100, 0, 100, 0, 100)),
        ],
        new Dictionary<int, WeddingRingEntry> { [39500] = new(39500, 5020, 5021), [39509] = new(39509, 5038, 5039) },
        new Dictionary<int, WeddingDressEntry> { [39000] = new(39000, 1, 1) },
        [],
        new HashSet<int> { 558, 559 },
        [7100],
        [5020],
        new Dictionary<int, CoupleEventPoints>());

    [Fact]
    public void NpcResultsFollowTheCoupleState()
    {
        Assert.Equal(2, CoupleRules.MarriageStateResult(CoupleStates.Joined));
        Assert.Equal(2, CoupleRules.MarriageStateResult(CoupleStates.OptionsChosen));
        Assert.Equal(1, CoupleRules.MarriageStateResult(CoupleStates.Married));
        Assert.Equal(4, CoupleRules.MarriageStateResult(CoupleStates.DivorceCancelled));
        Assert.Equal(3, CoupleRules.MarriageStateResult(CoupleStates.Divorced));
        Assert.Equal(4, CoupleRules.MarriageStateResult(CoupleStates.NoCouple));
        Assert.Equal(3, CoupleRules.MarriageOptionResult(CoupleStates.Joined));
        Assert.Equal(1, CoupleRules.MarriageOptionResult(CoupleStates.CloseEnough));
        Assert.Equal(2, CoupleRules.MarriageOptionResult(CoupleStates.OptionsChosen));
        Assert.Null(CoupleRules.MarriageOptionResult(CoupleStates.Married));
        Assert.True(CoupleRules.CanMakeCouple(CoupleStates.NoCouple, 30));
        Assert.False(CoupleRules.CanMakeCouple(CoupleStates.NoCouple, 29));
        Assert.False(CoupleRules.CanMakeCouple(CoupleStates.Joined, 99));
        Assert.False(CoupleRules.IsCoupleFunction(227));
        Assert.False(CoupleRules.IsCoupleFunction(233));
        Assert.True(CoupleRules.IsCoupleFunction(225));
    }

    [Fact]
    public void ExchangeListKeepsRowsInsideThePointsAndHeldCoupons()
    {
        var tables = Tables();
        var rows = CoupleRules.ExchangeList(tables, new CouplePoints(90, 100, 100), _ => 0);
        Assert.Equal([39509], rows.Select(row => row.ResId));
        rows = CoupleRules.ExchangeList(tables, new CouplePoints(90, 50, 50), id => id == 39000 ? 1 : 0);
        Assert.Equal([39000, 39500], rows.Select(row => row.ResId));
        Assert.Equal([12], CoupleRules.TitleList(tables, new CouplePoints(90, 50, 50)).Select(title => title.Index));
    }

    [Fact]
    public void OptionChecksRunInTheServerOrder()
    {
        var tables = Tables();
        var points = new CouplePoints(90, 50, 50);
        var request = new MarriageOptionRequest(0, 12, 39500, 39000);
        Assert.Equal(MarriageOptionRefusal.NotCloseEnough, CoupleRules.Choose(tables, CoupleStates.Joined, points, request, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.TitleMissing, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { TitleIndex = 99 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.TitleOutOfRange, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { TitleIndex = 1 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.RingMissing, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { RingItemId = 0 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.RingMissing, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { RingItemId = 39999 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.RingOutOfRange, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { RingItemId = 39509 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.DressMissing, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request with { DressItemId = 39999 }, _ => 1, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.DressCoupon, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request, _ => 0, 100000).Refusal);
        Assert.Equal(MarriageOptionRefusal.Money, CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request, _ => 1, 29999).Refusal);
        var (refusal, choice) = CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request, _ => 1, 30000);
        Assert.Equal(MarriageOptionRefusal.None, refusal);
        Assert.Equal(30000, choice!.Fee);
        Assert.Equal([(39000, 1)], choice.Coupons());
    }

    [Fact]
    public void ARingWithoutADressCostsNothingAndTakesNoDressCoupon()
    {
        var tables = Tables();
        var points = new CouplePoints(90, 50, 50);
        var request = new MarriageOptionRequest(0, 12, 39500, 0);
        var (refusal, choice) = CoupleRules.Choose(tables, CoupleStates.CloseEnough, points, request, _ => 0, 0);
        Assert.Equal(MarriageOptionRefusal.None, refusal);
        Assert.Null(choice!.Dress);
        Assert.Equal(0, choice.Fee);
        Assert.Empty(choice.Coupons());
    }

    [Theory]
    [InlineData(MarriageOptionRefusal.TitleMissing, 1u)]
    [InlineData(MarriageOptionRefusal.RingMissing, 1u)]
    [InlineData(MarriageOptionRefusal.DressMissing, 1u)]
    [InlineData(MarriageOptionRefusal.TitleOutOfRange, 2u)]
    [InlineData(MarriageOptionRefusal.RingOutOfRange, 2u)]
    [InlineData(MarriageOptionRefusal.DressOutOfRange, 2u)]
    [InlineData(MarriageOptionRefusal.RingCoupon, 3u)]
    [InlineData(MarriageOptionRefusal.DressCoupon, 3u)]
    [InlineData(MarriageOptionRefusal.Money, 4u)]
    [InlineData(MarriageOptionRefusal.NotCloseEnough, 5u)]
    public void RefusalsMapToTheServerCodes(MarriageOptionRefusal refusal, uint code)
    {
        Assert.Equal(code, CoupleRules.RefusalCode(refusal));
    }

    [Fact]
    public void AGrantedChoiceHasNoRefusalCode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CoupleRules.RefusalCode(MarriageOptionRefusal.None));
    }

    [Fact]
    public void CoupleSkillComesFromTheRingBySex()
    {
        var tables = Tables();
        Assert.Equal(5020, CoupleRules.CoupleSkillOf(tables, 39500, female: false));
        Assert.Equal(5021, CoupleRules.CoupleSkillOf(tables, 39500, female: true));
        Assert.Null(CoupleRules.CoupleSkillOf(tables, 0, female: true));
        Assert.True(CoupleRules.IsFemale(Species.Bunny));
        Assert.False(CoupleRules.IsFemale(Species.Buffalo));
        Assert.Equal((2UL, 1UL), CoupleRules.Order(1, true, 2, false));
        Assert.Equal((1UL, 2UL), CoupleRules.Order(1, false, 2, true));
    }
}
