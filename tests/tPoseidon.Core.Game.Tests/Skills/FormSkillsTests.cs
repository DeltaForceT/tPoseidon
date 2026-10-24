using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class FormSkillsTests
{
    private static readonly int[] BeastOrder = { 0, 2, 10, 11, 8 };
    private static readonly int[] ChangeOrder = { 0, 2, 8, 10, 11 };

    private static SkillTableRow Row(Dictionary<string, string> columns) => new() { Table = "ESAction_Form", Level = 1, Columns = columns };

    [Fact]
    public void WildMutationRaisesAttackAndLowersTheRest()
    {
        var row = Row(new() { ["ApPRatio"] = "1.3", ["DxPValue"] = "1", ["DpMRatio"] = "0.2", ["HvMRatio"] = "0.2", ["LkMRatio"] = "0.3" });

        var deltas = FormSkills.MorphDeltas(MorphRule.WildMutation, row, 500, 40, 300, 100);

        Assert.Equal(new[] { SkillEffectKinds.StatAp, SkillEffectKinds.StatDx, SkillEffectKinds.StatDp, SkillEffectKinds.StatHv, SkillEffectKinds.StatLk }, deltas.Select(d => d.Stat));
        Assert.Equal(new[] { (int)(500 * (double)1.3f), -1, -(int)(300 * (double)0.2f), -(int)(100 * (double)0.2f), -(int)(40 * (double)0.3f) }, deltas.Select(d => d.Delta));
    }

    [Fact]
    public void BeastSetsDefenceAndAvoidToAFraction()
    {
        var row = Row(new() { ["AP_Ratio"] = "2.6", ["DX_Ratio"] = "1", ["DP_Ratio"] = "0.2", ["HV_Ratio"] = "0.2", ["LK_Ratio"] = "0.3" });

        var deltas = FormSkills.MorphDeltas(MorphRule.Beast, row, 500, 40, 300, 100).ToDictionary(d => d.Stat, d => d.Delta);

        Assert.Equal((int)(300 * (double)0.2f) - 300, deltas[SkillEffectKinds.StatDp]);
        Assert.Equal(-1, deltas[SkillEffectKinds.StatDx]);
        Assert.Equal(BeastOrder, FormSkills.MorphDeltas(MorphRule.Beast, row, 1, 1, 1, 1).Select(d => d.Stat));
        Assert.Equal(ChangeOrder, FormSkills.MorphDeltas(MorphRule.WildChange, row, 1, 1, 1, 1).Select(d => d.Stat));
    }

    [Fact]
    public void FormulasFollowTheServer()
    {
        Assert.Equal((uint)(1000 * (double)0.5f), FormSkills.HpAfterMorph(1000, 0.5f));
        Assert.Equal((int)(40 * 15 * (double)2.4f), FormSkills.BunkerPool(40, 2.4f));
        Assert.Equal(2, FormSkills.SoldierCards(2, 5, 0));
        Assert.Equal(6, FormSkills.SoldierCards(2, 5, FormSkills.RandomMax));
        var scaled = (float)((300 + 40 * 8) * (double)1.2f);
        Assert.Equal((int)(scaled * (double)(float)(4 / 6.0 + 0.1)), FormSkills.SoldierPower(300, 40, 1.2f, 4));
        Assert.Equal((int)((80 * 1.0 + (30 << 4)) * 1.5f * (2 / 2.0)), FormSkills.KnifePower(80, 1f, 30, 1.5f, 2, 2));
        Assert.Equal(1, FormSkills.KnifePower(0, 1f, 0, 1.5f, 2, 2));
        Assert.Equal((int)((80 * 1.0 + 30 * 10) * 1.3f), FormSkills.UpThrowPower(80, 1f, 30, 1.3f));
    }

    [Fact]
    public void KnivesSplitLandedHitsAndPowerThrowCountsOnlyAtLevelZeroOrBelow()
    {
        Assert.Equal(50, FormSkills.SplitKnife(new SkillHit(100, 2, SkillDamage.KindHit, 0, 0), 2).Damage);
        Assert.Equal(100, FormSkills.SplitKnife(new SkillHit(100, 2, SkillDamage.KindMiss, 0, 0), 2).Damage);
        Assert.Equal(1f, FormSkills.KnifeMultiplier(true, 5, 1.4f));
        Assert.Equal(1.4f, FormSkills.KnifeMultiplier(true, 0, 1.4f));
        Assert.Equal(1f, FormSkills.KnifeMultiplier(false, 0, 1.4f));
    }

    [Fact]
    public void KindsCarryTheirShapes()
    {
        Assert.True(SkillEffectKinds.Find(FormSkills.BunkerDo) is { AbsorbsHits: true, PoolFromDig: true, Tail: SkillEffectTail.CurrentThenMax });
        Assert.True(SkillEffectKinds.Find(FormSkills.BeastDo) is { Morph: MorphRule.Beast, TrailingMs: FormSkills.MorphTrailingMs });
        Assert.True(SkillEffectKinds.Find(FormSkills.Beast) is { SpreadsAs: FormSkills.BeastDo, TmOnSpread: true });
        Assert.True(SkillEffectKinds.Find(FormSkills.SummonGuardian) is { Tail: SkillEffectTail.ModeThenDuration });
        Assert.Equal(MorphRule.WildChange, FormSkills.MorphOf(FormSkills.WildChange));
        Assert.True(ObjectHits.IsTargeted(FormSkills.ChainOfKnives));
        Assert.True(GroundSkills.IsGround(FormSkills.UpThrow));
    }
}
