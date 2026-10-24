using System.Globalization;
using System.Text;
using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Game.Tests.Progression;

public sealed class ProgressionChartsTests
{
    private static string StatusRows(int rows)
    {
        var text = new StringBuilder();
        for (var statLevel = 1; statLevel <= rows; statLevel++)
        {
            for (var stat = 0; stat < StatusValueChart.StatCount; stat++)
            {
                text.Append(CultureInfo.InvariantCulture, $"{statLevel}\t4\t{statLevel * 100 + stat}.75\t");
            }
            text.Append("\r\n");
        }
        return text.ToString();
    }

    [Fact]
    public void LevelExpChart_KeepsTheThirdFieldOfEachRow()
    {
        var chart = LevelExpChart.Parse("1\t50\t500\t500\r\n2\t68\t680\t1180\r\n3\t98\t980\t2160\r\n");

        Assert.Equal(500, chart.NextExp(1));
        Assert.Equal(680, chart.NextExp(2));
        Assert.Equal(980, chart.NextExp(3));
        Assert.Equal(0, chart.NextExp(4));
        Assert.Equal(0, chart.NextExp(0));
        Assert.Equal(0, chart.NextExp(LevelExpChart.LastLevel + 1));
    }

    [Fact]
    public void LevelExpChart_StopsAtAFieldThatIsNotANumber()
    {
        var chart = LevelExpChart.Parse("1 50 500 500 2 x 680 1180 3 98 980 2160");

        Assert.Equal(500, chart.NextExp(1));
        Assert.Equal(0, chart.NextExp(2));
        Assert.Equal(0, chart.NextExp(3));
    }

    [Fact]
    public void StatusValueChart_TruncatesTheThirdFieldAndIsEmptyOutsideItsRows()
    {
        var chart = StatusValueChart.Parse(StatusRows(8));

        Assert.Equal(109, chart.Value(1, StatusValueChart.Hp));
        Assert.Equal(803, chart.Value(8, StatusValueChart.Mp));
        Assert.Equal(0, chart.Value(9, StatusValueChart.Hp));
        Assert.Equal(0, chart.Value(0, StatusValueChart.Hp));
        Assert.Equal(0, chart.Value(1, StatusValueChart.StatCount));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(9, 3)]
    [InlineData(28, 8)]
    public void StatusValueChart_AStatLevelIsEveryFourPoints(int points, int statLevel)
    {
        Assert.Equal(statLevel, StatusValueChart.StatLevel(points));
    }

    [Fact]
    public void StatusValueChart_ComputesEveryStatFromItsOwnPoints()
    {
        var chart = StatusValueChart.Parse(StatusRows(8));
        var points = new short[] { 0, 4, 8, 3, 0, 0, 0, 0, 0, 9, 0, 28 };

        var stats = chart.Compute(points);

        Assert.Equal(100, stats.Ap);
        Assert.Equal(201, stats.Ac);
        Assert.Equal(302, stats.Dx);
        Assert.Equal(103, stats.Mp);
        Assert.Equal(309, stats.Hp);
        Assert.Equal(811, stats.Hv);
    }

    [Fact]
    public void TmLevelChart_AnswersForLevelsZeroToFiveHundredOnly()
    {
        var chart = new TmLevelChart(new[] { (0, 320), (1, 460), (500, 9), (501, 7) });

        Assert.Equal(320, chart.NextTmExp(0));
        Assert.Equal(460, chart.NextTmExp(1));
        Assert.Equal(0, chart.NextTmExp(2));
        Assert.Equal(9, chart.NextTmExp(500));
        Assert.Equal(TmLevelChart.NoSuchLevel, chart.NextTmExp(501));
        Assert.Equal(TmLevelChart.NoSuchLevel, chart.NextTmExp(-1));
    }

    [Fact]
    public void StatGrowth_EachLevelAddsTheGroupWeightToItsThreeStats()
    {
        var points = new short[12];
        points[0] = 12;

        StatGrowth.Raise(points, new short[] { 2, 1, 4, 3 }, 3);

        Assert.Equal(new short[] { 18, 6, 6, 3, 3, 3, 12, 12, 12, 9, 9, 9 }, points);
    }

    [Fact]
    public void StatGrowth_WeightsThatDoNotAddUpToTenAreReplacedByTheTypeRow()
    {
        var own = new short[] { 2, 1, 4, 3 };
        var broken = new short[] { 0, 0, 0, 0 };

        Assert.Same(own, StatGrowth.WeightsInUse(own, 0));
        Assert.Equal(new short[] { 4, 2, 1, 3 }, StatGrowth.WeightsInUse(broken, 0));
        Assert.Equal(new short[] { 1, 4, 3, 2 }, StatGrowth.WeightsInUse(broken, 1));
        Assert.Equal(new short[] { 2, 3, 4, 1 }, StatGrowth.WeightsInUse(broken, 2));
        Assert.Equal(new short[] { 3, 1, 2, 4 }, StatGrowth.WeightsInUse(broken, 3));
        Assert.Equal(new short[] { 3, 2, 3, 2 }, StatGrowth.WeightsInUse(broken, 4));
        Assert.Equal(new short[] { 3, 2, 3, 2 }, StatGrowth.WeightsInUse(broken, -1));
    }
}
