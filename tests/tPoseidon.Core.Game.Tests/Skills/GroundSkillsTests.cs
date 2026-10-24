using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class GroundSkillsTests
{
    [Theory]
    [InlineData(GroundSkills.ManaStorm, GroundSkills.ManaStormHit, 7, 900, 0x80, 0)]
    [InlineData(GroundSkills.CleavingTerra, GroundSkills.EarthDivideHit, 7, 500, 8, 0x19)]
    [InlineData(GroundSkills.Earthquake, GroundSkills.EarthquakeDo, 5, 500, 8, 0x19)]
    public void EachGroundSkillSpawnsADelayedMagicHit(int skill, int hitObject, int victims, int delayMs, int element, int magicLoss)
    {
        var spawn = GroundSkills.SpawnOf(skill)!;
        var kind = SkillEffectKinds.Find(hitObject)!;

        Assert.Equal(hitObject, spawn.ObjectAction);
        Assert.Equal(victims, spawn.Limit);
        Assert.Equal(SkillPeriodicHit.DelayedStrike, kind.Periodic);
        Assert.Equal(delayMs, kind.PeriodMs);
        Assert.Equal(2 * delayMs, kind.FixedDurationMs);
        Assert.Equal(element, kind.StrikeElement);
        Assert.Equal(magicLoss, kind.StrikeMagicLoss);
        Assert.True(SkillActions.IsBuilt(skill));
    }

    [Fact]
    public void ObjectsWithoutAClassAreNotLimited()
    {
        var book = new SkillEffectBook(SkillEffectBook.MonsterCapacities);
        for (var index = 0; index < 20; index++)
        {
            Assert.NotNull(book.Register(new SkillEffect
            {
                ObjectId = (ushort)index, Kind = SkillEffectKinds.Find(GroundSkills.ManaStormHit)!, SkillId = 2009, EsClass = -1,
                CasterEntityId = 1, OwnerEntityId = 2, Amount = 700f, DurationMs = 1800, Mode = SkillEffectMode.Applied,
            }));
        }

        Assert.Equal(20, book.Snapshot().Count);
    }

    [Fact]
    public void GroundInputsUseAPoint()
    {
        Assert.True(GroundSkills.UsesPoint(6));
        Assert.True(GroundSkills.UsesPoint(10));
        Assert.False(GroundSkills.UsesPoint(1));
    }
}
