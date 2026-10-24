using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearBestShieldTests
{
    private static readonly int[] Bases = { 100, 50, 40, 30, 20, 10, 5, 8, 4, 900, 120, 60 };

    private static SkillEffect OnMember(float amount) => new()
    {
        ObjectId = 0x500,
        Kind = SkillEffectKinds.Find(SkillEffectKinds.BestShieldOnMember)!,
        SkillId = 4905,
        EsClass = 3,
        CasterEntityId = 1,
        OwnerEntityId = 2,
        Amount = amount,
        DurationMs = 150 * SkillEffect.MillisecondsPerSecond,
        Mode = SkillEffectMode.Applied,
    };

    [Fact]
    public void BestShieldSpreadsItsMemberEffect()
    {
        var cast = SkillEffectKinds.Find(SkillEffectKinds.BestShield)!;
        var member = SkillEffectKinds.Find(SkillEffectKinds.BestShieldOnMember)!;

        Assert.Equal(SkillEffectKinds.BestShieldOnMember, cast.SpreadsAs);
        Assert.False(cast.ChangesStat);
        Assert.Equal(SkillEffectKinds.StatDp, member.Stat);
        Assert.Equal("DpRatio", member.AmountColumn);
        Assert.Equal("PDpRatio", member.MemberAmountColumn);
    }

    [Fact]
    public void TheProjectedDefenceIsAPercentOfTheBestPartyDefence()
    {
        Assert.Equal(245f, SkillEffectBook.ProjectedDefence(350, 70f), 3);
        Assert.Equal(175f, SkillEffectBook.ProjectedDefence(350, 50f), 3);
    }

    [Fact]
    public void TheMemberEffectAddsItsProjectedDefenceToDp()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(OnMember(SkillEffectBook.ProjectedDefence(351, 70f)));

        var deltas = book.Deltas(Bases, _ => null);

        Assert.Equal(245, deltas[SkillEffectKinds.StatDp]);
        Assert.Equal(0, deltas[SkillEffectKinds.StatAp]);
    }
}
