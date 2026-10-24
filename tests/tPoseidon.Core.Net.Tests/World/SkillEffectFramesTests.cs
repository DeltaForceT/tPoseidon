using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SkillEffectFramesTests
{
    private static SkillEffect Effect(int actionId, sbyte esClass, ushort owner, SkillEffectMode mode) => new()
    {
        ObjectId = 0x0510,
        Kind = SkillEffectKinds.Find(actionId)!,
        SkillId = 4007,
        EsClass = esClass,
        CasterEntityId = 0x0301,
        OwnerEntityId = owner,
        Amount = 0.1f,
        DurationMs = 40 * SkillEffect.MillisecondsPerSecond,
        Mode = mode,
    };

    [Fact]
    public void ABuffNamesCasterOwnerAndObjectThenItsSecondsAndMode()
    {
        Assert.Equal(
            new byte[]
            {
                0x01, 0x03, 0x01, 0x03, 0x10, 0x05, 0x5E, 0x04, 0x02, 0x00, 0xA7, 0x0F, 0x00, 0x00,
                0x28, 0x00, 0x01,
            },
            SkillEffectFrames.Started(Effect(1118, 0, 0x0301, SkillEffectMode.Applied), 0x0301));
    }

    [Fact]
    public void ArmorBreakerAddsTheTargetBeforeTheModeAndOthersOnlyTheMode()
    {
        var armorBreaker = SkillEffectFrames.Started(Effect(1081, 1, 0x0201, SkillEffectMode.Failed), 0x0201);
        var shockvibe = SkillEffectFrames.Started(Effect(1098, 1, 0x0201, SkillEffectMode.Resisted), 0x0201);

        Assert.Equal(new byte[] { 0x01, 0x02, 0x02 }, armorBreaker[^3..]);
        Assert.Equal(17, armorBreaker.Length);
        Assert.Equal(new byte[] { 0x03 }, shockvibe[^1..]);
        Assert.Equal(15, shockvibe.Length);
        Assert.Equal(0x01, shockvibe[9]);
    }

    [Fact]
    public void TheSmallFramesCarryTheirFields()
    {
        Assert.Equal(new byte[] { 0x01, 0x0A, 0xF2, 0xFF, 0xFF, 0xFF }, SkillEffectFrames.StatChanged(10, -14));
        Assert.Equal(new byte[] { 0x37, 0x04 }, SkillEffectFrames.Ending(1079));
        Assert.Equal(new byte[] { 0x01, 0x03, 0x10, 0x05 }, SkillEffectFrames.Ended(0x0301, 0x0510));
        Assert.Equal(new byte[] { 0x04, 0xEB, 0x03, 0x00, 0x00 }, SkillEffectFrames.Evicted(1003));
    }

    [Fact]
    public void PolarBearBuffsCarryOnlyTheirDurationInMilliseconds()
    {
        var muscleUp = SkillEffectFrames.Started(Effect(1524, 0, 0x0301, SkillEffectMode.Applied), 0x0301);

        Assert.Equal(18, muscleUp.Length);
        Assert.Equal(new byte[] { 0xF4, 0x05 }, muscleUp[6..8]);
        Assert.Equal(new byte[] { 0x40, 0x9C, 0x00, 0x00 }, muscleUp[14..]);
    }

    [Fact]
    public void MentalBreakSendsTheVisibleFlagBeforeItsDuration()
    {
        var mentalBreak = SkillEffectFrames.Started(Effect(1526, 1, 0x0201, SkillEffectMode.Applied), 0x0201);

        Assert.Equal(19, mentalBreak.Length);
        Assert.Equal(new byte[] { 0x00, 0x40, 0x9C, 0x00, 0x00 }, mentalBreak[14..]);
    }
}
