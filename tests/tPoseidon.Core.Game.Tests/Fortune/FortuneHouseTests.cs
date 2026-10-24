using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Fortune;

public sealed class FortuneHouseTests
{
    private static readonly int[] Pool = [21001, 21002, 21003];
    private static readonly uint[] DealtIds = [21001, 21002, 21003, 21001, 21002, 21003, 21001, 21002, 21003, 21001];
    private static readonly uint[] PlacedIndices = [3, 7];
    private static readonly uint[] JokerSlotIndex = [10];
    private static readonly uint[] SameIndexTwice = [2, 2];
    private static readonly uint[] OneIndex = [1];
    private static readonly int[] StatGroupEvents = [0x24, 0x27];
    private static readonly int[] BasicIds = [21001, 21004];
    private static readonly int[] JokerRolls = [9000, 6000, 2500];
    private static readonly ulong[] PlacedUids = [0x55, 0x66];

    private static FortuneCardEntry Row(int id, float jokerProb) =>
        new(id, "AP", 1f, 0.1f, jokerProb, 3600, Array.Empty<FortuneCross>());

    [Fact]
    public void DealGivesTenUniformBasicCardsAndTheJokerSlot()
    {
        var next = 0;
        var cards = FortuneHouse.Deal(Pool, count => next++ % count);
        Assert.Equal(FortuneRules.CardCount, cards.Length);
        Assert.Equal(DealtIds, cards.Take(FortuneRules.JokerSlot).Select(card => card.CardId));
        Assert.All(cards.Take(FortuneRules.JokerSlot), card => Assert.Equal((1, 0.099f), (card.IsBasic, card.Weight)));
        Assert.Equal(new FortuneCard(0, 0, FortuneHouse.JokerCardId, 0, 0.01f), cards[FortuneRules.JokerSlot]);
        Assert.Equal(0x3DCAC083u, BitConverter.SingleToUInt32Bits(cards[0].Weight));
    }

    [Fact]
    public void BasicCardsAreType35SubType70WithCardNumUpToThree()
    {
        var items = new[]
        {
            new ItemParamEntry { Id = 21001, Type = 35, SubType = 70, CardNum = 0 },
            new ItemParamEntry { Id = 21004, Type = 35, SubType = 70, CardNum = 3 },
            new ItemParamEntry { Id = 21005, Type = 35, SubType = 70, CardNum = 4 },
            new ItemParamEntry { Id = 21109, Type = 35, SubType = 71, CardNum = 0 },
        };
        Assert.Equal(BasicIds, FortuneHouse.BasicCards(items));
    }

    [Fact]
    public void JokerRangesAreNormalisedWhenTheSumIsAboveOne()
    {
        var ranges = FortuneHouse.JokerRanges(new[] { Row(21001, 0.3f), Row(21002, 0f), Row(21003, 0.6f), Row(21004, 0.1f), Row(21005, 1f) });
        Assert.Equal(new FortuneJokerRange(21001, 0, 1500), ranges[0]);
        Assert.Equal(new FortuneJokerRange(21003, 1500, 4500), ranges[1]);
        Assert.Equal(new FortuneJokerRange(21004, 4500, 5000), ranges[2]);
        Assert.Equal(new FortuneJokerRange(21005, 5000, 10000), ranges[3]);
    }

    [Fact]
    public void JokerCardRollsAgainBelowAFullTable()
    {
        var ranges = FortuneHouse.JokerRanges(new[] { Row(21001, 0.2f), Row(21002, 0.3f) });
        Assert.Equal(5000, ranges[^1].RangeHi);
        var rolls = new Queue<int>(JokerRolls);
        Assert.Equal(21002u, FortuneHouse.JokerCard(ranges, _ => rolls.Dequeue()));
        Assert.Null(FortuneHouse.JokerCard(Array.Empty<FortuneJokerRange>(), _ => 0));
    }

    [Fact]
    public void JokerExchangeReplacesOnlyFlaggedResults()
    {
        var draw = new FortuneDraw(new[] { new FortuneResult(10, 21109, true), new FortuneResult(2, 21003, false), new FortuneResult(10, 21109, true) }, true);
        var exchanged = FortuneHouse.ExchangeJokers(draw, _ => 21044);
        Assert.Equal(new FortuneResult(10, 21044, true), exchanged.Results[0]);
        Assert.Equal(new FortuneResult(2, 21003, false), exchanged.Results[1]);
        Assert.Equal(21044u, exchanged.Results[2].CardId);
        var plain = draw with { JokerHit = false };
        Assert.Same(plain, FortuneHouse.ExchangeJokers(plain, _ => 1));
    }

    [Fact]
    public void FeeIsTheNextUseRowAndTheLastRowPastTheTable()
    {
        var fees = new Dictionary<int, int> { [1] = 1000, [2] = 2000, [3] = 4000 };
        Assert.Equal(1000, FortuneHouse.Fee(fees, 1));
        Assert.Equal(4000, FortuneHouse.Fee(fees, 3));
        Assert.Equal(4000, FortuneHouse.Fee(fees, 9));
        Assert.Equal(0, FortuneHouse.Fee(new Dictionary<int, int>(), 1));
    }

    [Fact]
    public void ChangePlacesJokersOnBasicCardsOnlyOncePerIndex()
    {
        var round = new FortuneRound();
        Assert.Equal(FortuneChangeRefusal.WrongStep, round.Change(Array.Empty<FortuneJoker>(), Array.Empty<uint>()));
        round.Deal(FortuneHouse.Deal(Pool, _ => 0));
        var jokers = new[] { new FortuneJoker(0x55, 21044), new FortuneJoker(0x66, 21013) };
        Assert.Equal(FortuneChangeRefusal.CountMismatch, round.Change(jokers, OneIndex));
        Assert.Equal(FortuneChangeRefusal.NotABasicCard, round.Change(jokers.Take(1).ToArray(), JokerSlotIndex));
        Assert.Equal(FortuneChangeRefusal.DuplicateIndex, round.Change(jokers, SameIndexTwice));
        Assert.Null(round.Change(jokers, PlacedIndices));
        Assert.Equal(FortuneStep.Changed, round.Step);
        Assert.Equal(new FortuneCard(1, 1, 21044, 0x55, 0.099f), round.Cards[3]);
        Assert.Equal(new FortuneCard(1, 1, 21013, 0x66, 0.099f), round.Cards[7]);
        Assert.Equal(PlacedUids, round.JokerUids);
        Assert.Null(round.Change(Array.Empty<FortuneJoker>(), Array.Empty<uint>()));
        round.Close();
        Assert.False(round.InHouse);
        Assert.Empty(round.JokerUids);
    }

    [Fact]
    public void StatEventsFollowTheNonZeroStatGroups()
    {
        Assert.Equal(StatGroupEvents, FortuneHouse.StatEvents(FortuneRules.Parse("AC|3 HV|1 RFIT|2")));
        Assert.Empty(FortuneHouse.StatEvents(FortuneRules.Parse("EXP|5")));
    }
}
