using tPoseidon.Core.Game.Stats;

namespace tPoseidon.Core.Game.Tests.Stats;

public sealed class NaturalRecoveryTests
{
    private static readonly RecoveryAmounts Standing = new(5, 5);

    [Fact]
    public void TheFirstTickRecoversAndTheNextOneWaitsTwoSeconds()
    {
        var recovery = new NaturalRecovery();

        Assert.Equal(new RecoveryReport(105, 45), recovery.Tick(0, 100, 180, 40, 80, Standing));
        Assert.Equal(default, recovery.Tick(1999, 105, 180, 45, 80, Standing));
        Assert.Equal(new RecoveryReport(110, 50), recovery.Tick(1, 105, 180, 45, 80, Standing));
    }

    [Fact]
    public void RecoveryStopsAtTheMaximum()
    {
        var recovery = new NaturalRecovery();

        Assert.Equal(new RecoveryReport(180, 80), recovery.Tick(0, 178, 180, 79, 80, Standing));
    }

    [Fact]
    public void AFullValueIsReportedThreeTimesThenNotAgainUntilItRecovers()
    {
        var recovery = new NaturalRecovery();

        Assert.Equal(new RecoveryReport(180, 80), recovery.Tick(0, 180, 180, 80, 80, Standing));
        Assert.Equal(new RecoveryReport(180, 80), recovery.Tick(2000, 180, 180, 80, 80, Standing));
        Assert.Equal(new RecoveryReport(180, 80), recovery.Tick(2000, 180, 180, 80, 80, Standing));
        Assert.Equal(default, recovery.Tick(2000, 180, 180, 80, 80, Standing));
        Assert.Equal(new RecoveryReport(175, null), recovery.Tick(2000, 170, 180, 80, 80, Standing));
        Assert.Equal(new RecoveryReport(180, null), recovery.Tick(2000, 180, 180, 80, 80, Standing));
    }

    [Fact]
    public void DamageHoldsRecoveryForSixSecondsWithoutRestartingTheCountdown()
    {
        var recovery = new NaturalRecovery();
        recovery.Tick(0, 100, 180, 40, 80, Standing);
        recovery.Tick(1500, 105, 180, 45, 80, Standing);

        recovery.PauseAfterDamage();

        Assert.Equal(default, recovery.Tick(5999, 105, 180, 45, 80, Standing));
        Assert.Equal(default, recovery.Tick(1, 105, 180, 45, 80, Standing));
        Assert.Equal(default, recovery.Tick(499, 105, 180, 45, 80, Standing));
        Assert.Equal(new RecoveryReport(110, 50), recovery.Tick(1, 105, 180, 45, 80, Standing));
    }

    [Theory]
    [InlineData(0, NaturalRecovery.StandAction, 5, 5)]
    [InlineData(0, NaturalRecovery.SitAction, 15, 10)]
    [InlineData(0, NaturalRecovery.LieAction, 30, 20)]
    [InlineData(1, NaturalRecovery.SitAction, 10, 15)]
    [InlineData(1, NaturalRecovery.LieAction, 20, 30)]
    [InlineData(2, NaturalRecovery.SitAction, 15, 15)]
    [InlineData(2, NaturalRecovery.LieAction, 30, 30)]
    [InlineData(3, NaturalRecovery.SitAction, 15, 10)]
    [InlineData(3, NaturalRecovery.LieAction, 30, 20)]
    public void AmountsDependOnTheCharacterTypeAndThePosture(int characterType, byte action, int hp, int mp)
    {
        Assert.Equal(new RecoveryAmounts(hp, mp), NaturalRecovery.AmountsFor(characterType, action));
    }

    [Fact]
    public void OtherActionsAndUnknownTypesLeaveTheAmountsAlone()
    {
        Assert.Null(NaturalRecovery.AmountsFor(0, 0x05));
        Assert.Null(NaturalRecovery.AmountsFor(4, NaturalRecovery.SitAction));
    }
}
