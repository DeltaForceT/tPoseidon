using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PolarBearEffectsTests
{
    private static SkillEffect Effect(int actionId, float amount, float flat = 0f, ushort caster = 1) => new()
    {
        ObjectId = 100,
        Kind = SkillEffectKinds.Find(actionId)!,
        SkillId = 4700,
        EsClass = 0,
        CasterEntityId = caster,
        OwnerEntityId = 2,
        Amount = amount,
        Flat = flat,
        DurationMs = 60000,
        Mode = SkillEffectMode.Applied,
    };

    private static int[] Bases()
    {
        var bases = new int[SkillEffectKinds.StatCount];
        bases[SkillEffectKinds.StatAp] = 300;
        bases[SkillEffectKinds.StatDp] = 200;
        bases[SkillEffectKinds.StatDa] = 50;
        return bases;
    }

    [Fact]
    public void AttackBuffsFromDifferentSkillsAdd()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1524, 0.07f));
        book.Register(Effect(1541, 0.02f));

        var deltas = book.Deltas(Bases(), _ => null);

        Assert.Equal(27, deltas[SkillEffectKinds.StatAp]);
    }

    [Fact]
    public void ShieldOfWillAddsItsRevisionOnTopOfThePercent()
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(Effect(1527, 0.40f, 50f));

        Assert.Equal((int)Math.Truncate(200 * (double)0.40f) + 50, book.Deltas(Bases(), _ => null)[SkillEffectKinds.StatDp]);
    }

    [Fact]
    public void BearsRaidLowersDefenceByTheCastersAttack()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        book.Register(Effect(1543, 0.40f));

        var deltas = book.Deltas(Bases(), id => id == 1 ? new SkillEffectCaster(500, 0) : null, ownerIsPlayer: false);

        Assert.Equal(-(int)Math.Truncate(500 * (double)0.40f), deltas[SkillEffectKinds.StatDp]);
        Assert.True(SkillEffectKinds.Find(1543)!.RollsChance);
    }

    [Fact]
    public void LastDefenceIsCastOnlyAtOrBelowItsHpShare()
    {
        var kind = SkillEffectKinds.Find(1542)!;

        Assert.True(SkillEffectBook.AllowsCast(kind, 0.30f, 300, 1000));
        Assert.False(SkillEffectBook.AllowsCast(kind, 0.30f, 301, 1000));
        Assert.True(SkillEffectBook.AllowsCast(SkillEffectKinds.Find(1524)!, 0f, 1000, 1000));
    }
}
