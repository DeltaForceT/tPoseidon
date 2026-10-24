using tPoseidon.Core.Game.CardBattle;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class CardBattleRewardFramesTests
{
    [Fact]
    public void TheStatusReplyRoundTripsTheRewardBlock()
    {
        var reward = new CardBattleRewardBlock(52, true, 2425, 123456, 30001, 7, 24, 30002, 8, 9);
        var reply = CardBattleFrames.StatusReply(0x1122334455667788, 3, CardBattleFrames.StatusBagFull, true, reward, true);
        Assert.Equal(CardBattleFrames.StatusReplySize, reply.Length);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, reply[..4]);
        Assert.Equal(1, reply[0x12]);
        Assert.Equal(1, reply[^1]);
        var read = CardBattleFrames.ReadStatusReply(reply);
        Assert.Equal((0x1122334455667788UL, (ushort)3, CardBattleFrames.StatusBagFull, reward), read);
    }

    [Fact]
    public void TheStatusOrderNamesTheScoreAndTheFlags()
    {
        var data = new byte[CardBattleFrames.StatusSize];
        data[10] = 4;
        BitConverter.GetBytes((short)15).CopyTo(data, 11);
        data[0x11] = 0;
        BitConverter.GetBytes(30001u).CopyTo(data, 0x12);
        data[0x16] = 1;
        BitConverter.GetBytes(0x0102030405060708UL).CopyTo(data, 0x17);
        data[0x29] = 1;
        data[0x2A] = 1;
        BitConverter.GetBytes(30009u).CopyTo(data, 0x23);
        data[0x27] = 1;
        data[0x28] = 1;
        BitConverter.GetBytes((short)8).CopyTo(data, 0x2B);
        BitConverter.GetBytes(30010u).CopyTo(data, 0x1F);
        BitConverter.GetBytes(30011u).CopyTo(data, 0x2D);
        BitConverter.GetBytes(30012u).CopyTo(data, 0x31);
        var status = CardBattleFrames.ReadStatus(data)!.Value.Status;
        Assert.Equal(new CardBattleStatus(4, 15, false, 30001, true, 30009, true, true, 8, false)
        {
            LostCardItemId = 30010,
            OpponentCharacterId = 0x0102030405060708UL,
            OpponentHoldsSet = true,
            OpponentUsedChance = true,
            SecondWonCardItemId = 30011,
            SecondLostCardItemId = 30012,
        }, status);
    }
}
