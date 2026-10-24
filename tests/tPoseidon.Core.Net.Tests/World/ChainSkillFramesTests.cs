using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ChainSkillFramesTests
{
    private static readonly ChainVictim TwoHits = new(0x2000, new[]
    {
        new SkillHit(120, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0),
        new SkillHit(0, SkillDamage.PhysicalType, SkillDamage.KindMiss, 0, 0),
    });

    [Fact]
    public void PunchTailsCarryTheTargetBothPointsAndTenByteSlots()
    {
        var tail = SkillFrames.ChainTailBytes(ChainTail.TargetPointsThenSlots, 0x2000, (10, 20), (30, 40), new[] { TwoHits });

        Assert.Equal(10 + 2 * SkillFrames.ChainSlotSize, tail.Length);
        Assert.Equal(0x2000, BitConverter.ToUInt16(tail, 0));
        Assert.Equal(10, BitConverter.ToUInt16(tail, 2));
        Assert.Equal(40, BitConverter.ToUInt16(tail, 8));
        Assert.Equal(120, BitConverter.ToInt32(tail, 10));
        Assert.Equal(0xFFFF, BitConverter.ToUInt16(tail, 18));
        Assert.Equal(SkillDamage.KindMiss, BitConverter.ToInt32(tail, 24));
    }

    [Fact]
    public void ComboTailsAreSlotsOnly()
    {
        var tail = SkillFrames.ChainTailBytes(ChainTail.SlotsOnly, 0x2000, (10, 20), (30, 40), new[] { TwoHits });

        Assert.Equal(2 * SkillFrames.ChainSlotSize, tail.Length);
    }

    [Fact]
    public void BlazingTailsListEachVictimWithItsHitsAndAShieldFlag()
    {
        var tail = SkillFrames.ChainTailBytes(ChainTail.VictimsWithSlots, 0x2000, (10, 20), (30, 40), new[] { TwoHits, TwoHits with { EntityId = 0x2001 } });

        Assert.Equal(1 + 2 * (2 + 1 + 2 * SkillFrames.ChainSlotSize + 1), tail.Length);
        Assert.Equal(2, tail[0]);
        Assert.Equal(0x2000, BitConverter.ToUInt16(tail, 1));
        Assert.Equal(2, tail[3]);
        Assert.Equal(0, tail[24]);
        Assert.Equal(0x2001, BitConverter.ToUInt16(tail, 25));
    }
}
