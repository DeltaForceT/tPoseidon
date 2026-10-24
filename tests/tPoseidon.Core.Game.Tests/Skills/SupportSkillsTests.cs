using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class SupportSkillsTests
{
    private static readonly int[] Cleansed = { 1099, 1098 };

    [Fact]
    public void FirstAidHealsHalfOfMagicAndLuckTimesRatio()
    {
        Assert.Equal((int)((300 + 100) * 0.5 * 4.0f), SupportSkills.FirstAidHeal(300, 100, 4.0f));
    }

    [Fact]
    public void HeartsGraceAddsAShareOfMaxHpAndOfTheCastersMagic()
    {
        Assert.Equal((int)(2000 * (double)0.07f) + (int)(300 * (double)0.07f * 40.0), SupportSkills.GraceHeal(2000, 300, 0.07f));
    }

    [Fact]
    public void AHealOnlyCountsWhenThePatientWasHurt()
    {
        Assert.Equal(new HealResult(1000, true), SupportSkills.Heal(900, 1000, 500));
        Assert.Equal(new HealResult(1000, false), SupportSkills.Heal(1000, 1000, 500));
    }

    [Fact]
    public void ManaConvertTradesAShareOfMaxHpForMana()
    {
        var conversion = SupportSkills.ConvertMana(1500, 2000, 100, 1000, 0.22f, 0.9f);

        Assert.True(conversion.Applied);
        Assert.Equal((uint)(1500 - (long)(2000 * (double)0.22f)), conversion.Hp);
        Assert.Equal((uint)(100 + (long)(2000 * (double)0.22f * 0.9f)), conversion.Mp);
        Assert.False(SupportSkills.ConvertMana(400, 2000, 100, 1000, 0.22f, 0.9f).Applied);
    }

    [Fact]
    public void CleansesReadTheActionColumnsAndSkipTheSentinel()
    {
        var row = new SkillTableRow
        {
            Table = "ESAction_PrimaryCure", Level = 1,
            Columns = new Dictionary<string, string> { ["ESAction0"] = "1099", ["ESAction1"] = "1098", ["ESAction2"] = "1000" },
        };

        Assert.Equal(Cleansed, SupportSkills.CleansedBy(row, SupportSkills.BasicHealingEntries));
    }

    [Fact]
    public void SelfCureCreatesAHealingDoThatCleansesFirst()
    {
        var instant = SkillEffectKinds.Find(SupportSkills.SelfCure)!;
        var cure = SkillEffectKinds.Find(SupportSkills.SelfCureDo)!;

        Assert.Equal(SkillInstant.SelfCure, instant.Instant);
        Assert.Equal(SkillPeriodicHit.HealOverTime, cure.Periodic);
        Assert.Equal("Heal", cure.HealColumn);
        Assert.Equal(1000, cure.TickScale);
        Assert.Equal(5, cure.CleansesFromRow);
        Assert.True(SupportSkills.TargetsDead(SupportSkills.Revival));
        Assert.True(SkillActions.IsBuilt(SupportSkills.BasicHealing));
    }
}
