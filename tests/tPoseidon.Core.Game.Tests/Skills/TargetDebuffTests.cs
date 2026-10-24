using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class TargetDebuffTests
{
    private static readonly int[] Bases = { 400, 50, 12, 30, 20, 10, 5, 8, 4, 900, 120, 60 };

    [Fact]
    public void HardenAddsTwiceTheBaseDxAndHalvesNormalHits()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.Harden)!;
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(new SkillEffect
        {
            ObjectId = 0x500, Kind = kind, SkillId = 2125, EsClass = 1, CasterEntityId = 1, OwnerEntityId = 2,
            Amount = 10f, DurationMs = 10000, Mode = SkillEffectMode.Applied,
        });

        Assert.Equal(24, book.Deltas(Bases, _ => null, ownerIsPlayer: false)[SkillEffectKinds.StatDx]);
        Assert.True(kind.HalvesHits);
        Assert.True(kind.OnTarget);
        Assert.True(kind.CarriesMode);
    }

    [Fact]
    public void DarkSlowSlowsByDownValueOverOneHundred()
    {
        var kind = SkillEffectKinds.Find(SkillEffectKinds.DarkSlow)!;

        Assert.Equal("DownValue", kind.SlowColumn);
        Assert.Equal(0.35f, 35f * kind.SlowScale, 4);
        Assert.False(kind.ChangesStat);
    }
}
