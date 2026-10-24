using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class QuestResultManifestPacketTests
{
    private static readonly byte[] CapturedBunny1233Complete = new byte[]
    {
        0x04, 0x00,
        0x04, 0x02, 0xD6, 0x0B, 0x00, 0x00, 0x12, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x04, 0x02, 0xBE, 0x0F, 0x00, 0x00, 0x13, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x04, 0x02, 0x82, 0x19, 0x00, 0x00, 0x14, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x01, 0x1D, 0x2E, 0x00, 0x00, 0x11, 0xFE, 0xC8, 0x05, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x03, 0x00,
        0xD6, 0x0B, 0x00, 0x00, 0x01, 0x00,
        0xBE, 0x0F, 0x00, 0x00, 0x01, 0x00,
        0x82, 0x19, 0x00, 0x00, 0x01, 0x00,
        0x00, 0x01, 0x00, 0x00,
        0x00, 0x01, 0x00, 0x00,
        0xD1, 0x04, 0x00, 0x00,
        0x01, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };

    [Fact]
    public void Encode_RevokeFirstInput_ReordersToRewardsBeforeRevoke()
    {
        var packet = new QuestResultManifestPacket
        {
            CompletedQuestId = 1233,
            Records = new[]
            {
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.ItemRevoke,
                    ItemIndex = 11805,
                    ItemUid = 0x05C8FE11,
                    Count = 0,
                },
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.RewardGrant,
                    ItemIndex = 3030,
                    ItemUid = 0x05C8FE12,
                    Count = 1,
                },
            },
        };

        var encoded = packet.EncodePayload();
        Assert.Equal(0x04, encoded[2]);
        Assert.Equal(0x02, encoded[3]);
        Assert.Equal(0x03, encoded[23]);
        Assert.Equal(0x01, encoded[24]);
    }

    [Fact]
    public void Encode_Bunny1233CompleteSample_MatchesCapturedBytesExactly()
    {
        Assert.Equal(119, CapturedBunny1233Complete.Length);

        var packet = new QuestResultManifestPacket
        {
            CompletedQuestId = 1233,
            Records = new[]
            {
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.RewardGrant,
                    ItemIndex = 3030,
                    ItemUid = 0x05C8FE12,
                    Count = 1,
                },
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.RewardGrant,
                    ItemIndex = 4030,
                    ItemUid = 0x05C8FE13,
                    Count = 1,
                },
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.RewardGrant,
                    ItemIndex = 6530,
                    ItemUid = 0x05C8FE14,
                    Count = 1,
                },
                new QuestResultRecord
                {
                    Kind = QuestResultRecordKind.ItemRevoke,
                    ItemIndex = 11805,
                    ItemUid = 0x05C8FE11,
                    Count = 0,
                },
            },
        };

        var encoded = packet.EncodePayload();

        Assert.Equal(CapturedBunny1233Complete.Length, encoded.Length);
        for (var i = 0; i < CapturedBunny1233Complete.Length; i++)
        {
            Assert.True(
                CapturedBunny1233Complete[i] == encoded[i],
                $"Byte {i} differs: captured=0x{CapturedBunny1233Complete[i]:X2} encoded=0x{encoded[i]:X2}");
        }
    }
}
