using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Game.Tests.PvP;

public sealed class PkRulesTests
{
    [Fact]
    public void ZoneTypeSevenAndOption0x40AreKillZones()
    {
        Assert.Equal(new PkZoneEntry(PkRules.NoPvp, PkRules.RecordOff, false), PkRules.ZoneEntry(4, 0x10));
        Assert.Equal(new PkZoneEntry(PkRules.PkZoneMode, PkRules.RecordOff, true), PkRules.ZoneEntry(7, 0));
        Assert.Equal(new PkZoneEntry(PkRules.PkZoneMode, PkRules.RecordPeace, true), PkRules.ZoneEntry(4, 0x40 | 0x10));
    }

    [Fact]
    public void ToggleAndDeathLoss()
    {
        Assert.Equal(PkRules.RecordSkirmish, PkRules.Toggle(true));
        Assert.Equal(PkRules.RecordPeace, PkRules.Toggle(false));
        Assert.True(PkRules.LosesExpOnDeath(PkRules.NoPvp));
        Assert.False(PkRules.LosesExpOnDeath(PkRules.PkZoneMode));
    }
}
