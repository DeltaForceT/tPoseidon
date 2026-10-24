using tPoseidon.Core.Game.Progression;

namespace tPoseidon.Core.Game.Tests.Progression;

public sealed class StatPointSpendingTests
{
    private static short[] Amounts(params (int Stat, short Amount)[] spent)
    {
        var amounts = new short[StatPointSpending.StatCount];
        foreach (var (stat, amount) in spent) amounts[stat] = amount;
        return amounts;
    }

    [Fact]
    public void ReadAmounts_TakesTwelveSixteenBitValues()
    {
        var request = new byte[StatPointSpending.RequestSize];
        request[0] = 3;
        request[18] = 0xFF;
        request[19] = 0xFF;

        var amounts = StatPointSpending.ReadAmounts(request);

        Assert.Equal(3, amounts[0]);
        Assert.Equal(-1, amounts[9]);
        Assert.Throws<ArgumentException>(() => StatPointSpending.ReadAmounts(new byte[23]));
    }

    [Fact]
    public void TheAmountsAreAddedAndTakenFromTheBonusPoints()
    {
        var points = new short[] { 18, 6, 6, 3, 3, 3, 12, 12, 12, 9, 9, 9 };

        var spent = StatPointSpending.TrySpend(points, 12, Amounts((0, 4), (9, 8)), out var left);

        Assert.True(spent);
        Assert.Equal(0, left);
        Assert.Equal(new short[] { 22, 6, 6, 3, 3, 3, 12, 12, 12, 17, 9, 9 }, points);
    }

    [Fact]
    public void MoreThanTheBonusPointsIsRefusedAndNothingChanges()
    {
        var points = new short[StatPointSpending.StatCount];

        var spent = StatPointSpending.TrySpend(points, 3, Amounts((0, 2), (1, 2)), out var left);

        Assert.False(spent);
        Assert.Equal(3, left);
        Assert.All(points, p => Assert.Equal(0, p));
    }

    [Fact]
    public void ANegativeAmountIsRefused()
    {
        var points = new short[StatPointSpending.StatCount];

        Assert.False(StatPointSpending.TrySpend(points, 10, Amounts((0, 5), (1, -1)), out _));
        Assert.All(points, p => Assert.Equal(0, p));
    }

    [Fact]
    public void SpendingNothingIsAccepted()
    {
        var points = new short[StatPointSpending.StatCount];

        Assert.True(StatPointSpending.TrySpend(points, 0, Amounts(), out var left));
        Assert.Equal(0, left);
    }
}
