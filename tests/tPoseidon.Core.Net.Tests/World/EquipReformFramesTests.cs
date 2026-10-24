using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class EquipReformFramesTests
{
    private static readonly byte[] RemovedRecord =
    [
        3, 0, 0, 0,
        1, 0, 0, 0,
        0x9C, 0xF6, 0, 0,
        0x22, 0, 0, 0, 1, 0, 0, 0,
        0, 0,
        0, 0, 0, 0,
        0, 0,
    ];

    private static readonly byte[] RareRecord =
    [
        1, 0, 0, 0,
        2, 0, 0, 0,
        0xDB, 0x53, 1, 0,
        0x11, 0, 0, 0, 1, 0, 0, 0,
        1, 0,
        1, 2, 0, 3,
        0, 0,
    ];

    [Fact]
    public void RawRecordIsTwentyEightBytesWithTheFourRareBytesAlwaysPresent()
    {
        Assert.Equal(RemovedRecord, QuestResultFrames.RawItemOperation(new BagOperation(3, 1, 63132, 0x1_0000_0022, 0)));
        Assert.Equal(RareRecord, QuestResultFrames.RawItemOperation(new BagOperation(1, 2, 87003, 0x1_0000_0011, 1, MixSlot: 3, RefineType: 1, RefineLevel: 2)));
    }

    [Fact]
    public void RequestIsFourUidsAndTwoStatChoices()
    {
        byte[] payload =
        [
            0x11, 0, 0, 0, 1, 0, 0, 0,
            0x22, 0, 0, 0, 1, 0, 0, 0,
            0x33, 0, 0, 0, 1, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
            2, 0, 0, 0, 0x43, 0x4D, 0, 0,
            0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0,
            (byte)'a', 0, (byte)'b', 0,
        ];
        Assert.True(EquipReformFrames.TryReadRequest(payload, out var request));
        Assert.Equal(0x1_0000_0011UL, request.TargetWireUid);
        Assert.Equal(0x1_0000_0022UL, request.MaterialWireUid);
        Assert.Equal(0x1_0000_0033UL, request.FirstElixirWireUid);
        Assert.Equal(0UL, request.SecondElixirWireUid);
        Assert.Equal(new ReformStatChoice(2, 19779), request.First);
        Assert.False(request.Second.IsChosen);
        Assert.False(EquipReformFrames.TryReadRequest(payload.AsSpan(0, 40), out _));
    }

    [Fact]
    public void ResultCarriesRecordsUidHeadPropertiesMoneyAndRefine()
    {
        var frame = EquipReformFrames.Result(
            new BagOperation(3, 1, 63132, 0x1_0000_0022, 0),
            null,
            0x1_0000_0011,
            87003,
            [new ItemProperty(10, 10000, 156, 0)],
            1234,
            new ReformRefineBonus(10000, 15));
        byte[] tail =
        [
            1,
            0x11, 0, 0, 0, 1, 0, 0, 0,
            0xDB, 0x53, 1, 0,
            1, 0, 0, 0,
            10, 0, 0, 0, 0x10, 0x27, 0x9C, 0, 0, 0, 0, 0,
            0xD2, 4, 0, 0,
            0x10, 0x27, 15, 0,
        ];
        Assert.Equal([.. RemovedRecord, .. tail], frame);
    }

    [Fact]
    public void ResultWithAnElixirCarriesTheSecondRecord()
    {
        var frame = EquipReformFrames.Result(
            new BagOperation(3, 1, 63132, 0x1_0000_0022, 0),
            new BagOperation(1, 2, 87003, 0x1_0000_0011, 1, MixSlot: 3, RefineType: 1, RefineLevel: 2),
            0x1_0000_0011,
            87003,
            [],
            0,
            ReformRefineBonus.None);
        Assert.Equal(28 + 1 + 28 + 8 + 4 + 4 + 4 + 4, frame.Length);
        Assert.Equal(0, frame[28]);
        Assert.Equal(RareRecord, frame.AsSpan(29, 28).ToArray());
    }

    [Fact]
    public void RefusalIsCodeThenMoneyAndResetDoneIsTwoUids()
    {
        Assert.Equal([0x0E, 0, 0, 0, 0x10, 0x27, 0, 0], EquipReformFrames.Refused(0x0E, 10000));
        Assert.Equal([0x11, 0, 0, 0, 1, 0, 0, 0, 0x44, 0, 0, 0, 1, 0, 0, 0], EquipReformFrames.ResetDone(0x1_0000_0011, 0x1_0000_0044));
        byte[] reset = [0x11, 0, 0, 0, 1, 0, 0, 0, 0x44, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(EquipReformFrames.TryReadResetRequest(reset, out var request));
        Assert.Equal(new ReformCountResetRequest(0x1_0000_0011, 0x1_0000_0044), request);
    }
}
