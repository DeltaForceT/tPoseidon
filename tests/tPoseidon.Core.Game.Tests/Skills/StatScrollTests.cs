using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class StatScrollTests
{
    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_S_APUpScroll", Level = 1, Columns = columns };

    [Fact]
    public void LevelScrollsScaleTheBigStatsByFourTenthsAndTheSmallOnesByAFortieth()
    {
        var attack = Row(new() { ["CalculateType"] = "2", ["ApUpRatio"] = "10" });
        var luck = Row(new() { ["CalculateType"] = "2", ["LkUpRatio"] = "10" });
        var power = Row(new() { ["CalculateType"] = "2", ["ApUpRatio"] = "7", ["AcUpRatio"] = "7", ["DxUpRatio"] = "7" });

        Assert.Equal((SkillEffectKinds.StatAp, 400), Assert.Single(ScrollSkills.StatDeltas(attack, 100)));
        Assert.Equal((SkillEffectKinds.StatLk, 25), Assert.Single(ScrollSkills.StatDeltas(luck, 100)));
        Assert.Empty(ScrollSkills.StatDeltas(luck, 1));
        Assert.Equal(new[] { (SkillEffectKinds.StatAp, 280), (SkillEffectKinds.StatAc, 17) }, ScrollSkills.StatDeltas(power, 100));
    }

    [Fact]
    public void FlatScrollsAddTheTruncatedRatio()
    {
        var flat = Row(new() { ["CalculateType"] = "1", ["HvUpRatio"] = "6.9" });

        Assert.Equal((SkillEffectKinds.StatHv, 6), Assert.Single(ScrollSkills.StatDeltas(flat, 50)));
    }

    [Fact]
    public void WideHealingAddsHalfTheMaximumCappedAtTheMaximum()
    {
        Assert.Equal(1500u, ScrollSkills.Healed(1000, 2000, 0.25f));
        Assert.Equal(2000u, ScrollSkills.Healed(1500, 2000, 0.5f));
    }

    [Fact]
    public void ScrollKindsAreRegistered()
    {
        Assert.True(SkillEffectKinds.Find(ScrollSkills.StatUpScroll) is { SpreadsAs: ScrollSkills.StatUpScrollDo, PartySpreadColumn: ScrollSkills.PartySkillTypeColumn });
        Assert.True(SkillEffectKinds.Find(ScrollSkills.StatUpScrollDo) is { ScrollStats: true, Tail: SkillEffectTail.ModeThenStrings });
        Assert.True(SkillEffectKinds.Find(ScrollSkills.RainOfHeart) is { Stat: SkillEffectKinds.StatLk, GrantsTm: false });
        Assert.True(SupportSkills.TargetsPlayers(ScrollSkills.RainOfHeart));
        Assert.True(GroundSkills.IsGround(ScrollSkills.PartyDetector));
    }
}
