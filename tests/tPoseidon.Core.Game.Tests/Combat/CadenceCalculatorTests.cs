using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Game.Tests.Combat;

public class CadenceCalculatorTests
{
    private static CadenceCalculator NewCalc(ServerRates? rates = null) => new(rates ?? new ServerRates());

    [Fact]
    public void Player_ZeroDx_ReturnsBaseMs()
    {
        var calc = NewCalc();
        Assert.Equal(1900, calc.PlayerSwingCadenceMs(dx: 0));
    }

    [Fact]
    public void Player_PositiveDx_ReducesCadence()
    {
        var calc = NewCalc();
        Assert.Equal(1800, calc.PlayerSwingCadenceMs(dx: 5));
        Assert.Equal(1700, calc.PlayerSwingCadenceMs(dx: 10));
    }

    [Fact]
    public void Player_HugeDx_ClampsToMin()
    {
        var calc = NewCalc();
        Assert.Equal(600, calc.PlayerSwingCadenceMs(dx: 9999));
    }

    [Fact]
    public void Player_NegativeDx_ClampsToMax()
    {
        var calc = NewCalc();
        Assert.Equal(3000, calc.PlayerSwingCadenceMs(dx: -100));
    }

    [Fact]
    public void Mob_ZeroAttackSpeed_ReturnsBaseMs()
    {
        var calc = NewCalc();
        Assert.Equal(1500, calc.MobStrikeCadenceMs(0));
    }

    [Fact]
    public void Mob_HighAttackSpeed_ClampsToMin()
    {
        var calc = NewCalc();
        Assert.Equal(400, calc.MobStrikeCadenceMs(9999));
    }

    [Fact]
    public void RatesSnapshot_IsHotSwappable()
    {
        var rates = new ServerRates();
        var calc = new CadenceCalculator(() => rates);
        Assert.Equal(1900, calc.PlayerSwingCadenceMs(0));

        rates = new ServerRates
        {
            Cadence = new ServerRates.CadenceSection { PlayerBaseMs = 600 }
        };
        calc = new CadenceCalculator(() => rates);
        Assert.Equal(600, calc.PlayerSwingCadenceMs(0));
    }

    [Fact]
    public void CadenceSection_DefaultsMatchSection96()
    {
        var c = new ServerRates().Cadence;
        Assert.Equal(1900, c.PlayerBaseMs);
        Assert.Equal(1500, c.MobBaseMs);
        Assert.Equal(20, c.MsPerDxPoint);
        Assert.Equal(600, c.MinPlayerMs);
        Assert.Equal(3000, c.MaxPlayerMs);
        Assert.Equal(400, c.MinMobMs);
        Assert.Equal(4000, c.MaxMobMs);
    }
}
