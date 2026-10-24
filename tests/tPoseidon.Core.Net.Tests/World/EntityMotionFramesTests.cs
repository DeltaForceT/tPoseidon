using tPoseidon.Core.Common;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class EntityMotionFramesTests
{
    [Fact]
    public void TheEmoteFrameCarriesTheEntityIdAndTheCode()
    {
        Assert.Equal(new byte[] { 0x01, 0x03, 0x05 }, EntityMotionFrames.Emote(0x0301, 5));
    }

    [Fact]
    public void ThePostureFrameCarriesTheEntityIdAndTheCode()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x0D }, EntityMotionFrames.Posture(0x1234, 0x0D));
    }

    [Theory]
    [InlineData(Opcode.ENTITY_MOVE)]
    [InlineData(Opcode.C2S_OP_001D_TBD)]
    [InlineData(Opcode.C2S_ITEM_PICKUP)]
    [InlineData(Opcode.C2S_DRILL_START)]
    [InlineData(Opcode.C2S_OP_0266_TBD)]
    public void RestingSwallowsMovementAttackPickupDrillingAndTheItemRequest(Opcode opcode)
    {
        Assert.True(PostureInput.IsSwallowed(opcode));
    }

    [Theory]
    [InlineData(Opcode.C2S_REST_ACTION)]
    [InlineData(Opcode.ENTITY_EMOTE)]
    [InlineData(Opcode.C2S_OP_00EE_TBD)]
    public void RestingLetsPostureEmoteAndSkillRequestsThrough(Opcode opcode)
    {
        Assert.False(PostureInput.IsSwallowed(opcode));
    }

    [Fact]
    public void OnlyTheSkillRequestIsASkillRequest()
    {
        Assert.True(PostureInput.IsSkillRequest(Opcode.C2S_OP_00EE_TBD));
        Assert.False(PostureInput.IsSkillRequest(Opcode.ENTITY_MOVE));
    }
}
