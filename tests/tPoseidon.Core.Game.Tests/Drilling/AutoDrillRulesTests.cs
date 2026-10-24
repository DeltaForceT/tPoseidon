using tPoseidon.Core.Game.Drilling;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class AutoDrillRulesTests
{
    private static readonly Dictionary<int, int> Pets = new() { [1566] = 3000 };

    [Fact]
    public void ModeErrorsFollowTheServerOrder()
    {
        Assert.Equal(AutoDrillRules.ModeOff, AutoDrillRules.ModeError(false, 1, null, Pets));
        Assert.Equal(AutoDrillRules.MapRefuses, AutoDrillRules.ModeError(true, 0x201, null, Pets));
        Assert.Equal(AutoDrillRules.NoAutoDrillPet, AutoDrillRules.ModeError(true, 0x200, null, Pets));
        Assert.Equal(AutoDrillRules.NoAutoDrillPet, AutoDrillRules.ModeError(true, 0, 1557, Pets));
        Assert.Null(AutoDrillRules.ModeError(true, 0, 1566, Pets));
    }

    [Fact]
    public void CoolTimeIsTwoThirdsOfTheRow()
    {
        Assert.True(AutoDrillRules.IsBeforeCoolTime(3000, 1999));
        Assert.False(AutoDrillRules.IsBeforeCoolTime(3000, 2000));
    }

    [Fact]
    public void AutoModeHalvesTheFindTmExp()
    {
        Assert.Equal(1201, AutoDrillRules.FindTmExp(2403, true));
        Assert.Equal(2403, AutoDrillRules.FindTmExp(2403, false));
    }

    [Fact]
    public void PickableCountIsZeroWhenTheFreeWeightCannotHoldOne()
    {
        Assert.Equal(0, AutoDrillRules.PickableCount(100, 100, 0, 5));
        Assert.Equal(0, AutoDrillRules.PickableCount(100, 95, 6, 5));
        Assert.Equal(2, AutoDrillRules.PickableCount(100, 90, 4, 5));
        Assert.Equal(5, AutoDrillRules.PickableCount(100, 90, 0, 5));
    }
}
