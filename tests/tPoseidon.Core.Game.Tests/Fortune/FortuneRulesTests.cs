using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Fortune;

public sealed class FortuneRulesTests
{
    private static FortuneCardEntry Card(int id, string attribute, float value, params (string, float)[] cross) =>
        new(id, attribute, value, 0.3f, 0.6f, 3600, cross.Select(pair => new FortuneCross(pair.Item1, pair.Item2)).ToArray());

    private static readonly FortuneTables Tables = new(
        new Dictionary<int, FortuneCardEntry>
        {
            [21003] = Card(21003, "AP", 16f, ("AP", 0f), ("AC", 1f), ("DX", -0.33f), ("MD", 16f)),
            [21013] = Card(21013, "AC", 1f),
            [21110] = Card(21110, "RFIT", 2f),
        },
        new Dictionary<int, int> { [1] = 1000 },
        new Dictionary<string, float>(),
        new[]
        {
            new FortuneTitleRule(6, new[] { new FortuneTitleCondition(2, 21110), new FortuneTitleCondition(0, 0), new FortuneTitleCondition(15, 2) }, 133),
            new FortuneTitleRule(11, new[] { new FortuneTitleCondition(2, 21013), new FortuneTitleCondition(0, 0), new FortuneTitleCondition(15, 3) }, 1),
            new FortuneTitleRule(12, new[] { new FortuneTitleCondition(1, 0), new FortuneTitleCondition(0, 0), new FortuneTitleCondition(15, 3) }, 2),
        });

    private static readonly int[] ExpectedRanges = [2500, 5000, 10000];
    private static readonly float[] RangeWeights = [1f, 1f, 2f];
    private static readonly int[] Picks = [0, 1, 2];
    private static readonly int[] Rolls = [0, 500, 9999];

    private static FortuneResult[] Results(params uint[] ids) => ids.Select((id, i) => new FortuneResult(i, id, false)).ToArray();

    [Fact]
    public void ParseSumsNamedPairsIntoTheirSlots()
    {
        var bonus = FortuneRules.Parse("AP|16 AC|1\tAP|8 RFIT|2 DROP|3\r\nEXP|5 DX|-0");
        Assert.Equal(24, bonus.Stats[0]);
        Assert.Equal(1, bonus.Stats[1]);
        Assert.Equal(0, bonus.Stats[2]);
        Assert.Equal((2, 3, 5), (bonus.Refine, bonus.Drop, bonus.Exp));
        Assert.Equal(0, FortuneRules.Parse(string.Empty).Stats.Sum());
    }

    [Fact]
    public void AwardJoinsCardTokensAndSumsDurations()
    {
        var award = FortuneRules.Award(Results(21003, 21013, 21110), Tables);
        Assert.Equal("AP|16 AC|1 RFIT|2", award.Text);
        Assert.Equal(3 * 3600, award.DurationSeconds);
        Assert.False(award.ThreeOfAKind);
    }

    [Fact]
    public void ThreeOfAKindAppendsTheNonZeroCrossValues()
    {
        var award = FortuneRules.Award(Results(21003, 21003, 21003), Tables);
        Assert.True(award.ThreeOfAKind);
        Assert.Equal("AP|16 AP|16 AP|16 AC|1 DX|-0 MD|16", award.Text);
    }

    [Fact]
    public void LowestMatchingIndexGivesTheTitleAndUndeterminedRowsStopTheSearch()
    {
        Assert.Equal((133, true), FortuneRules.Title(Results(21110, 21003, 21110), Tables));
        Assert.Equal((1, true), FortuneRules.Title(Results(21013, 21013, 21013), Tables));
        Assert.Equal((0, false), FortuneRules.Title(Results(21003, 21013, 21110), Tables));
    }

    [Fact]
    public void RangesAreCumulativeTenThousandths()
    {
        Assert.Equal(ExpectedRanges, FortuneRules.Ranges(RangeWeights));
    }

    [Fact]
    public void DrawLowersADrawnCardAndMarksTheJokerSlot()
    {
        var cards = Enumerable.Range(0, FortuneRules.CardCount)
            .Select(i => new FortuneCard(0, i == FortuneRules.JokerSlot ? (ushort)0 : (ushort)1, (uint)(21001 + i), 0, i == FortuneRules.JokerSlot ? 0.01f : 0.099f))
            .ToArray();
        var rolls = new Queue<int>(Rolls);
        var draw = FortuneRules.Draw(0, Picks, cards, 0.3f, 1f, _ => 0f, _ => rolls.Dequeue());
        Assert.Equal(0, draw.Results[0].CardIndex);
        Assert.Equal(1, draw.Results[1].CardIndex);
        Assert.Equal(FortuneRules.JokerSlot, draw.Results[2].CardIndex);
        Assert.True(draw.JokerHit);
        Assert.True(draw.Results[2].Joker);
    }
}
