using System.Buffers.Binary;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Fortune;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class FortuneFramesTests
{
    [Fact]
    public void RefusedIsAFourByteCode()
    {
        Assert.Equal(new byte[] { 2, 0, 0, 0 }, FortuneFrames.Refused(FortuneFrames.RefusedNoMoney));
    }

    [Fact]
    public void AppliedFrameIsSourceByteThenTwoBlocks()
    {
        var bonus = FortuneRules.Parse("AP|16 HV|2 RFIT|3 DROP|4 EXP|5");
        var block = new FortuneBlock(2, DateTimeOffset.FromUnixTimeSeconds(1000), DateTimeOffset.FromUnixTimeSeconds(4600), 133, bonus);
        var frame = FortuneFrames.Applied(FortuneFrames.AppliedByAward, block);

        Assert.Equal(229, frame.Length);
        Assert.Equal(1, frame[0]);
        var a = frame.AsSpan(1, FortuneFrames.BlockSize);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(a));
        Assert.Equal(1000, BinaryPrimitives.ReadInt64LittleEndian(a[0x04..]));
        Assert.Equal(4600, BinaryPrimitives.ReadInt64LittleEndian(a[0x0C..]));
        Assert.Equal(133u, BinaryPrimitives.ReadUInt32LittleEndian(a[0x1C..]));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(a[0x24..]));
        Assert.Equal(2, BinaryPrimitives.ReadInt16LittleEndian(a[0x3A..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt16LittleEndian(a[0x68..]));
        Assert.Equal(4, BinaryPrimitives.ReadInt16LittleEndian(a[0x6C..]));
        Assert.Equal(5, BinaryPrimitives.ReadInt16LittleEndian(a[0x6E..]));
        Assert.All(frame[(1 + FortuneFrames.BlockSize)..], b => Assert.Equal(0, b));
        Assert.Equal(0, FortuneFrames.Applied(FortuneFrames.AppliedByStarTears, block)[0]);
    }

    [Fact]
    public void MasterInfoRoundTripsAndAbsentStaysAbsent()
    {
        var info = new FortuneInfo
        {
            CharacterId = 9,
            ViewCount = 3,
            TitleId = 133,
            ItemAttrString = "AP|16 AC|1",
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1_700_003_600),
        };
        Assert.Equal(info, FortuneMessages.DecodeInfo(9, FortuneMessages.EncodeInfo(info)));
        Assert.Null(FortuneMessages.DecodeInfo(9, FortuneMessages.EncodeInfo(null)));
    }
    private static readonly byte[] ChangeRequest =
    [
        1, 0, 0, 0, 2, 0, 0, 0,
        0x55, 0, 0, 0, 0, 0, 0, 0,
        0x66, 0, 0, 0, 0, 0, 0, 0,
        2, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0,
    ];

    private static readonly byte[] RollRequest =
    [
        0x10, 0x27, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 11, 0, 0, 0,
    ];

    private static readonly ulong[] ChangedUids = [0x55, 0x66];
    private static readonly uint[] ChangedIndices = [3, 7];
    private static readonly int[] ClampedPicks = [0, 4, 10];
    private static readonly byte[] CardsHeader = [2, 0, 11];
    private static readonly int[] OnePool = [21001];

    [Fact]
    public void CardsFrameIsUsesExchangeCountThenRawRecords()
    {
        var cards = FortuneHouse.Deal(OnePool, _ => 0);
        cards[3] = cards[3] with { JokerSet = 1, CardId = 21044, ItemUid = 0x1122334455667788 };
        var frame = FortuneFrames.Cards(2, 0, cards);

        Assert.Equal(3 + 11 * 0x1C, frame.Length);
        Assert.Equal(CardsHeader, frame[..3]);
        var joker = frame.AsSpan(3 + 3 * 0x1C, 0x1C);
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(joker));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(joker[2..]));
        Assert.Equal(21044u, BinaryPrimitives.ReadUInt32LittleEndian(joker[4..]));
        Assert.Equal(0x1122334455667788ul, BinaryPrimitives.ReadUInt64LittleEndian(joker[8..]));
        Assert.Equal(0x3DCAC083u, BinaryPrimitives.ReadUInt32LittleEndian(joker[0x10..]));
        var slot = frame.AsSpan(3 + 10 * 0x1C, 0x1C);
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(slot[2..]));
        Assert.Equal(21109u, BinaryPrimitives.ReadUInt32LittleEndian(slot[4..]));
        Assert.Equal(0.01f, BinaryPrimitives.ReadSingleLittleEndian(slot[0x10..]));
        Assert.Equal(frame[2..], FortuneFrames.CardsChanged(cards));
    }

    [Fact]
    public void ChangeAndRollRequestsAreRead()
    {
        Assert.True(FortuneFrames.TryReadChange(ChangeRequest, out var change));
        Assert.Equal(ChangedUids, change.JokerUids);
        Assert.Equal(ChangedIndices, change.Indices);
        Assert.False(FortuneFrames.TryReadChange(ChangeRequest.AsSpan(0, 20), out _));

        Assert.True(FortuneFrames.TryReadRoll(RollRequest, out var roll));
        Assert.Equal(5000, roll.Power);
        Assert.Equal(ClampedPicks, roll.Picks);
        Assert.False(FortuneFrames.TryReadRoll(RollRequest.AsSpan(0, 12), out _));
    }

    [Fact]
    public void RolledFrameCarriesBothDrawsDurationTitleUsesAndAward()
    {
        var before = new[] { new FortuneResult(1, 21002, false), new FortuneResult(10, 21109, true), new FortuneResult(4, 21011, false) };
        var final = new[] { before[0], new FortuneResult(10, 21044, true), before[2] };
        var frame = FortuneFrames.Rolled(new FortuneRollFrame(false, before, final, 10800, 133, 3, "AP|16 AC|1"));

        Assert.Equal(1 + 0x24 + 0x24 + 8 + 4 + 4 + 2 + 10, frame.Length);
        Assert.Equal(0, frame[0]);
        Assert.Equal(10u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1 + 12)));
        Assert.Equal(21109u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1 + 12 + 4)));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1 + 12 + 8)));
        Assert.Equal(21044u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(1 + 0x24 + 12 + 4)));
        var tail = frame.AsSpan(1 + 0x48);
        Assert.Equal(10800, BinaryPrimitives.ReadInt64LittleEndian(tail));
        Assert.Equal(133u, BinaryPrimitives.ReadUInt32LittleEndian(tail[8..]));
        Assert.Equal(3u, BinaryPrimitives.ReadUInt32LittleEndian(tail[12..]));
        Assert.Equal(10, BinaryPrimitives.ReadUInt16LittleEndian(tail[16..]));
        Assert.Equal("AP|16 AC|1"u8.ToArray(), tail[18..].ToArray());
    }

    [Fact]
    public void PaidFrameIsCharacterMoneyThenOperationAndUidPairs()
    {
        var take = new BagOperation(QuestResultFrames.StackChanged, 1, 21044, 0x55, 2);
        var frame = FortuneFrames.Paid(9, 123456, new[] { new FortunePaidJoker(take, 0x55) });

        Assert.Equal(8 + 4 + 1 + 16 + 8, frame.Length);
        Assert.Equal(9ul, BinaryPrimitives.ReadUInt64LittleEndian(frame));
        Assert.Equal(123456u, BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(8)));
        Assert.Equal(1, frame[12]);
        Assert.Equal(QuestResultFrames.ItemOperation(take), frame[13..29]);
        Assert.Equal(0x55ul, BinaryPrimitives.ReadUInt64LittleEndian(frame.AsSpan(29)));
    }

    [Fact]
    public void TitleAnnounceIsTheEntityAsU64ThenTheTitleId()
    {
        var frame = FortuneFrames.TitleAnnounce(0x0123, 0x00AABBCC);

        Assert.Equal(FortuneFrames.TitleAnnounceSize, frame.Length);
        Assert.Equal(0x0123UL, BitConverter.ToUInt64(frame, 0));
        Assert.Equal(0x00AABBCCu, BitConverter.ToUInt32(frame, 8));
    }
}
