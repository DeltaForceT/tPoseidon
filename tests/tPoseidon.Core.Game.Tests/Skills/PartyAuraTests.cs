using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PartyAuraTests
{
    private static readonly int[] Bases = { 400, 50, 12, 30, 20, 10, 5, 8, 4, 900, 120, 60 };

    [Theory]
    [InlineData(SkillEffectKinds.HeartBeat, SkillEffectKinds.HeartBeatOnMember, false)]
    [InlineData(SkillEffectKinds.ManaFastRecover, SkillEffectKinds.ManaFastRecoverOnMember, false)]
    [InlineData(SkillEffectKinds.BigShield, SkillEffectKinds.BigShieldOnMember, false)]
    [InlineData(SkillEffectKinds.CheerUp, SkillEffectKinds.CheerUpOnMember, true)]
    [InlineData(SkillEffectKinds.WhiteKnight, SkillEffectKinds.WhiteKnightOnMember, true)]
    [InlineData(SkillEffectKinds.GoodLuck, SkillEffectKinds.GoodLuckOnMember, true)]
    [InlineData(SkillEffectKinds.HawkEye, SkillEffectKinds.HawkEyeOnMember, true)]
    [InlineData(SkillEffectKinds.MultiAgilityUp, SkillEffectKinds.MultiAgilityUpOnMember, true)]
    public void EachAuraScansEveryFourHundredMillisecondsForItsChild(int parent, int child, bool grantsTm)
    {
        var aura = SkillEffectKinds.Find(parent)!;

        Assert.Equal(SkillPeriodicHit.AuraScan, aura.Periodic);
        Assert.Equal(400, aura.PeriodMs);
        Assert.Equal(child, aura.AuraChild);
        Assert.Equal(grantsTm, aura.GrantsTm);
        Assert.Equal(SkillEffectTail.Empty, aura.Tail);
        Assert.True(SkillEffectKinds.Find(child)!.IsAuraChild);
    }

    [Fact]
    public void BigShieldGrowsWithTheBolsteredPartySize()
    {
        Assert.Equal(0f, SkillEffectKinds.BigShieldBonus(0));
        Assert.Equal(0f, SkillEffectKinds.BigShieldBonus(1));
        Assert.Equal(0.03f, SkillEffectKinds.BigShieldBonus(2));
        Assert.Equal(0.48f, SkillEffectKinds.BigShieldBonus(6));
        Assert.Equal(0f, SkillEffectKinds.BigShieldBonus(7));

        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(new SkillEffect
        {
            ObjectId = 1, Kind = SkillEffectKinds.Find(SkillEffectKinds.BigShieldOnMember)!, SkillId = 4004, EsClass = 0,
            CasterEntityId = 1, OwnerEntityId = 2, Amount = 0.06f, Extra = 0.12f, DurationMs = 2000, Mode = SkillEffectMode.Applied,
        });

        Assert.Equal((int)(120 * (double)(0.06f + 0.12f)), book.Deltas(Bases, _ => null)[SkillEffectKinds.StatDp]);
    }

    [Fact]
    public void CheerUpCarriesItsRadiusAsAPercentOfSeventy()
    {
        Assert.Equal((float)(135 / 70.0f * 100.0), SkillEffectKinds.CheerUpTail(135));
        Assert.Equal(SkillEffectTail.RadiusPercent, SkillEffectKinds.Find(SkillEffectKinds.CheerUpOnMember)!.Tail);
    }

    [Fact]
    public void ARefreshedChildTakesTheParentsRemainingTime()
    {
        var child = new SkillEffect
        {
            ObjectId = 1, Kind = SkillEffectKinds.Find(SkillEffectKinds.HeartBeatOnMember)!, SkillId = 1002, EsClass = 0,
            CasterEntityId = 1, OwnerEntityId = 2, Amount = 0.06f, DurationMs = 30000, Mode = SkillEffectMode.Applied,
        };
        child.Tick(5000);

        child.Refresh(40000);

        Assert.Equal(40000, child.RemainingMs);
    }
}
