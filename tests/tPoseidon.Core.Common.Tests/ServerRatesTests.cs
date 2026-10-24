namespace tPoseidon.Core.Common.Tests;

public class ServerRatesTests
{
    [Fact]
    public void Default_AllRatesAreOnePointOh()
    {
        var r = ServerRates.Default();
        Assert.Equal(1.0, r.Global.ExpRate);
        Assert.Equal(1.0, r.Global.TmxpRate);
        Assert.Equal(1.0, r.Global.GalderRate);
        Assert.Equal(1.0, r.Global.DropRate);
    }

    [Fact]
    public void ScaleExp_OnePointOh_ReturnsBaseValue()
    {
        var r = ServerRates.Default();
        Assert.Equal(256, r.ScaleExp(256));
        Assert.Equal(1374, r.ScaleExp(1374));
    }

    [Fact]
    public void ScaleExp_FivePointOh_QuintuplesValue()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { ExpRate = 5.0 },
        };
        Assert.Equal(1280, r.ScaleExp(256));
        Assert.Equal(6870, r.ScaleExp(1374));
    }

    [Fact]
    public void ScaleExp_EventBonus_StacksAdditivelyOnTopOfBase()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { ExpRate = 1.0 },
            ExpHelper = new ServerRates.ExpHelperSection { ExpEvent = 0.5 },
        };
        Assert.Equal(384, r.ScaleExp(256));
    }

    [Fact]
    public void ScaleExp_EventAndGlobalCompound()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { ExpRate = 2.0 },
            ExpHelper = new ServerRates.ExpHelperSection { ExpEvent = 0.5, ExpPcRoom = 0.25 },
        };
        Assert.Equal(896, r.ScaleExp(256));
    }

    [Fact]
    public void ScaleExp_ZeroBase_StaysZero()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { ExpRate = 100.0 },
        };
        Assert.Equal(0, r.ScaleExp(0));
    }

    [Fact]
    public void ScaleExp_NegativeBase_StaysZero()
    {
        var r = ServerRates.Default();
        Assert.Equal(0, r.ScaleExp(-100));
    }

    [Fact]
    public void ScaleTmxp_UsesTmHelperBonusesNotExpBonuses()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { TmxpRate = 1.0 },
            ExpHelper = new ServerRates.ExpHelperSection
            {
                ExpEvent = 1.0,
                TmEvent = 0.0,
            },
        };
        Assert.Equal(256, r.ScaleTmxp(256));
    }

    [Fact]
    public void ScaleGalder_AppliesGlobalRateOnly()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { GalderRate = 3.0 },
        };
        Assert.Equal(6000, r.ScaleGalder(2000));
    }

    [Fact]
    public void ScaleDropChance_CapsAtOne()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { DropRate = 100.0 },
        };
        Assert.Equal(1.0, r.ScaleDropChance(0.5));
    }

    [Fact]
    public void ScaleDropChance_HalvesWhenRateBelowOne()
    {
        var r = new ServerRates
        {
            Global = new ServerRates.GlobalSection { DropRate = 0.5 },
        };
        Assert.Equal(0.05, r.ScaleDropChance(0.1), precision: 4);
    }

    [Fact]
    public void LoadFromFile_ReadsGlobalAndExpHelperSections()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                {
                  "Global": { "ExpRate": 5.0, "TmxpRate": 3.0, "GalderRate": 2.0, "DropRate": 1.5 },
                  "ExpHelper": { "EXP_PCRoom": 0.1, "EXP_Event": 0.2, "TM_PCRoom": 0.05, "TM_Event": 0.15 }
                }
                """);
            var r = ServerRates.LoadFromFile(path);
            Assert.Equal(5.0, r.Global.ExpRate);
            Assert.Equal(3.0, r.Global.TmxpRate);
            Assert.Equal(2.0, r.Global.GalderRate);
            Assert.Equal(1.5, r.Global.DropRate);
            Assert.Equal(0.1, r.ExpHelper.ExpPcRoom);
            Assert.Equal(0.2, r.ExpHelper.ExpEvent);
            Assert.Equal(0.05, r.ExpHelper.TmPcRoom);
            Assert.Equal(0.15, r.ExpHelper.TmEvent);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Provider_NoFile_FallsBackToDefaults()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), $"rates-missing-{Guid.NewGuid()}.json");
        using var p = new ServerRatesProvider(nonExistent, watchForChanges: false);
        Assert.Equal(1.0, p.Current.Global.ExpRate);
    }

    [Fact]
    public void Provider_Reload_PicksUpFileEdits()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"Global":{"ExpRate":2.0}}""");
            using var p = new ServerRatesProvider(path, watchForChanges: false);
            Assert.Equal(2.0, p.Current.Global.ExpRate);

            File.WriteAllText(path, """{"Global":{"ExpRate":7.5}}""");
            p.Reload();

            Assert.Equal(7.5, p.Current.Global.ExpRate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Provider_Reload_RaisesEvent()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{"Global":{"ExpRate":1.0}}""");
            using var p = new ServerRatesProvider(path, watchForChanges: false);
            ServerRates? captured = null;
            p.RatesReloaded += r => captured = r;

            File.WriteAllText(path, """{"Global":{"ExpRate":3.5}}""");
            p.Reload();

            Assert.NotNull(captured);
            Assert.Equal(3.5, captured!.Global.ExpRate);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Summary_DefaultRates_NoExpHelperBonusesShown()
    {
        var r = ServerRates.Default();
        var s = r.Summary();
        Assert.Contains("exp×1", s);
        Assert.DoesNotContain("bonuses", s);
    }

    [Fact]
    public void Summary_WithExpHelperBonus_IncludesBonusBlock()
    {
        var r = new ServerRates
        {
            ExpHelper = new ServerRates.ExpHelperSection { ExpEvent = 0.5 },
        };
        Assert.Contains("bonuses", r.Summary());
        Assert.Contains("exp_evt", r.Summary());
    }
}
