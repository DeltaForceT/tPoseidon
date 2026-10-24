using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Game.Tests.PvP;

public sealed class PvpHitRulesTests
{
    [Fact]
    public void OnlyPvpModesOneToFourMayHitPlayers()
    {
        Assert.False(PvpHitRules.CanAttackPlayer(0));
        Assert.True(PvpHitRules.CanAttackPlayer(1));
        Assert.True(PvpHitRules.CanAttackPlayer(4));
        Assert.False(PvpHitRules.CanAttackPlayer(5));
    }

    [Fact]
    public void HitRollIsCappedAtNinetyPercent()
    {
        Assert.True(PvpHitRules.Hits(50, 49));
        Assert.False(PvpHitRules.Hits(50, 50));
        Assert.True(PvpHitRules.Hits(500, 179));
        Assert.False(PvpHitRules.Hits(500, 180));
    }

    [Fact]
    public void KillCreditNeedsPkZoneRecordModeAndAPlayerKiller()
    {
        Assert.True(PvpHitRules.CreditsKill(PkRules.PkZoneMode, PkRules.RecordPeace, true));
        Assert.False(PvpHitRules.CreditsKill(PkRules.PkZoneMode, PkRules.RecordOff, true));
        Assert.False(PvpHitRules.CreditsKill(PkRules.PkZoneMode, PkRules.RecordSkirmish, false));
        Assert.False(PvpHitRules.CreditsKill(3, PkRules.RecordSkirmish, true));
        Assert.True(PvpHitRules.AnnouncesKill(PkRules.PkZoneMode, true));
    }

    [Fact]
    public void DailyKillCapStopsCounting()
    {
        Assert.Equal(new PkCounters(20, 120, 0, 0), PvpHitRules.KillerCredit(new PkCounters(19, 119, 0, 0), 20, 9999));
        Assert.Equal(new PkCounters(20, 120, 0, 0), PvpHitRules.KillerCredit(new PkCounters(20, 120, 0, 0), 20, 9999));
        Assert.Equal(new PkCounters(0, 0, 3, 8), PvpHitRules.VictimCredit(new PkCounters(0, 0, 2, 7), 9999));
        Assert.Equal(new PkCounters(0, 0, 3, 7), PvpHitRules.VictimCredit(new PkCounters(0, 0, 2, 7), 3));
    }

    [Fact]
    public void DamageFactorIsIndexedByTheAttackerPvpMode()
    {
        Assert.Equal(1.0f, PvpHitRules.DamageFactor(0));
        Assert.Equal(0.25f, PvpHitRules.DamageFactor(1));
        Assert.Equal(0.4f, PvpHitRules.DamageFactor(2));
        Assert.Equal(0.4f, PvpHitRules.DamageFactor(3));
        Assert.Equal(0.4f, PvpHitRules.DamageFactor(4));
    }

    [Fact]
    public void DamageTruncatesTheBaseBeforeTheFactor()
    {
        Assert.Equal(19, PvpHitRules.Damage(100, 30, 0.25f));
        Assert.Equal(30, PvpHitRules.Damage(100, 30, 0.4f));
        Assert.Equal(0, PvpHitRules.Damage(10, 30, 0.25f));
    }

    [Fact]
    public void DodgeRollIsCappedAt170()
    {
        Assert.True(PvpHitRules.Dodges(500, 169));
        Assert.False(PvpHitRules.Dodges(500, 170));
        Assert.False(PvpHitRules.Dodges(20, 20));
    }

    [Fact]
    public void OnlyAPeaceModeVictimInAPkZoneHitByAPlayerBreaksPeace()
    {
        Assert.True(PvpHitRules.BreaksPeace(PkRules.PkZoneMode, PkRules.RecordPeace, true));
        Assert.False(PvpHitRules.BreaksPeace(PkRules.PkZoneMode, PkRules.RecordSkirmish, true));
        Assert.False(PvpHitRules.BreaksPeace(PkRules.PkZoneMode, PkRules.RecordPeace, false));
        Assert.False(PvpHitRules.BreaksPeace(3, PkRules.RecordPeace, true));
    }

    [Fact]
    public void ProtectedVictimBlocks()
    {
        var outcome = PvpHitRules.Strike(500, 500, 1, 0, 0, 100, true, new Random(1));
        Assert.Equal(0, outcome.Damage);
        Assert.Equal(tPoseidon.Core.Game.Combat.MeleeResolution.ResultBlock, outcome.Result);
    }
}
