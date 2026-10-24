using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class UniqueItemFramesTests
{
    [Fact]
    public void WithdrawnIsU32CountThenOperations()
    {
        var removed = new BagOperation(QuestResultFrames.Removed, 2, 50000, 0x1_0000_0011, 0, 3, 1, 5, 0);
        var frame = UniqueItemFrames.Withdrawn(new[] { removed });
        Assert.Equal(1u, BitConverter.ToUInt32(frame, 0));
        Assert.Equal(QuestResultFrames.ItemOperation(removed), frame[4..]);
    }

    [Fact]
    public void WithdrawRequestCarriesThreeEntries()
    {
        var request = new UniqueWithdrawRequest(12, 0x3B5D, 0x99, UniqueItemMessages.WithdrawCallback, new[] { new UniqueHeldItem(0x11, 50000) });
        var body = UniqueItemMessages.EncodeWithdraw(request);
        Assert.Equal(2 + 2 + 8 + 2 + 3 * 12, body.Length);
        var decoded = UniqueItemMessages.DecodeWithdraw(body);
        Assert.Equal(request.CharUid, decoded.CharUid);
        Assert.Equal(request.ZoneId, decoded.ZoneId);
        Assert.Equal(request.PlayerId, decoded.PlayerId);
        Assert.Equal(request.Items, decoded.Items);
        var withdrawn = UniqueItemMessages.DecodeWithdrawn(UniqueItemMessages.EncodeWithdrawn(request.Items));
        Assert.Equal(request.Items, withdrawn);
        Assert.Equal((0x11UL, 50000), UniqueItemMessages.DecodePickup(UniqueItemMessages.EncodePickup(0x11, 50000)));
    }

    [Fact]
    public void BossKillAndDutyExpireCarryTheirFields()
    {
        Assert.Equal(new byte[] { 0xA6, 0x08 }, UniqueItemFrames.BossKilled(2214));
        var expire = UniqueItemFrames.DutyExpire(0x0102030405060708, 0x1112131415161718);
        Assert.Equal(16, expire.Length);
        Assert.Equal(0x0102030405060708L, BitConverter.ToInt64(expire, 0));
        Assert.Equal(0x1112131415161718L, BitConverter.ToInt64(expire, 8));
        Assert.Equal((0x99UL, (ushort)3001), UniqueItemMessages.DecodeBossKill(UniqueItemMessages.EncodeBossKill(0x99, 3001)));
        var end = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var now = end.AddDays(-7);
        Assert.Equal((0x99UL, end.ToFileTime(), now.ToFileTime()), UniqueItemMessages.DecodeDutyExpire(UniqueItemMessages.EncodeDutyExpire(0x99, end, now)));
    }

    [Fact]
    public void TheLatestBossKillsRoundTrip()
    {
        var at = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
        var body = tPoseidon.Core.InterServer.UniqueItemMessages.EncodeLatestBossKills(new[] { new tPoseidon.Core.Data.BossKillRecord(2072, at) });
        Assert.Equal(11, body.Length);
        var kills = tPoseidon.Core.InterServer.UniqueItemMessages.DecodeLatestBossKills(body);
        Assert.Equal(2072, Assert.Single(kills).MonsterId);
        Assert.Equal(at, kills[0].LatestKill);
    }

    [Fact]
    public void TheZoneUserCountAnswerGoesBackToTheAskingServerWithItsRequestId()
    {
        var routed = new InterServerFrame
        {
            Opcode = InterServerOpcode.McmdRequestZoneUserCount,
            Header = new InterServerHeader { SourceServerIndex = 2, DestServerIndex = 3, CharUid = 77, RequestId = 0 },
            Body = ZoneUserCountMessages.EncodeRouted(201, 0x1234),
        };
        var answer = MasterZoneUserCount.Answer(routed, 3, 12);
        Assert.Equal(InterServerOpcode.McmdRequestZoneUserCountAckOk, answer.Opcode);
        Assert.Equal((ushort)2, answer.Header.DestServerIndex);
        Assert.Equal((ushort)0x1234, answer.Header.RequestId);
        Assert.Equal(((ushort)201, 12), ZoneUserCountMessages.DecodeCount(answer.Body));
    }
}
