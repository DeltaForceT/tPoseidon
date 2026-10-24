using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyQuestFramesTests
{
    [Fact]
    public void TheQuestGivenFrameCarriesQuestMonsterTargetLimitAndCompletions()
    {
        var bytes = PartyQuestFrames.GivenFor(3006, 2094, 64, 1_200_000, 2);

        Assert.Equal(13, bytes.Length);
        Assert.Equal(3006u, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(2094, BitConverter.ToUInt16(bytes, 4));
        Assert.Equal(64, BitConverter.ToUInt16(bytes, 6));
        Assert.Equal(1_200_000u, BitConverter.ToUInt32(bytes, 8));
        Assert.Equal(2, bytes[12]);
    }

    [Fact]
    public void TheKillListSkipsEmptyMemberSlots()
    {
        var bytes = PartyQuestFrames.KillsFor(3, 64, new[] { (0x11UL, (ushort)2), (0UL, (ushort)0), (0x22UL, (ushort)1) });

        Assert.Equal(5 + 2 * 10, bytes.Length);
        Assert.Equal(3, BitConverter.ToUInt16(bytes, 0));
        Assert.Equal(64, BitConverter.ToUInt16(bytes, 2));
        Assert.Equal(2, bytes[4]);
        Assert.Equal(0x22UL, BitConverter.ToUInt64(bytes, 15));
    }

    [Fact]
    public void TheRewardPutsExpAndTmBeforeTheItemsAndTheQuestLast()
    {
        var bytes = QuestResultFrames.PartyQuestResult(Array.Empty<BagOperation>(), 1000, 200, 3006);

        Assert.Equal(new byte[] { 0xE8, 0x03, 0, 0, 0xC8, 0, 0, 0, 0, 0, 0xBE, 0x0B, 0, 0 }, bytes);
    }

    [Theory]
    [InlineData(153u, 0)]
    [InlineData(157u, 4)]
    [InlineData(158u, 0)]
    [InlineData(165u, 2)]
    [InlineData(172u, 4)]
    public void PartyQuestFunctionsTakeTheirSlotFromTheFunctionId(uint functionId, int slot)
    {
        Assert.Equal(slot, tPoseidon.Core.Game.Quests.NpcScript.Classify(functionId).Slot);
    }
}
