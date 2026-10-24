using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ItemComposingFramesTests
{
    private static readonly byte[] OpenedEquipment = [0, 0];
    private static readonly byte[] RefusedCatalyst = [0, 0, 0x0E, 0];
    private static readonly byte[] ClosedSeven = [7, 0];
    private static readonly byte[] PropertiesAndMoney =
    [
        12, 0x90, 0x65, 0xDB, 0x53, 0x01, 0x00,
        13, 0x91, 0x65, 0x11, 0x10, 0x00, 0x00,
        0x40, 0xE2, 0x01, 0x00,
    ];

    [Fact]
    public void RequestIsKindThenThreeUids()
    {
        byte[] payload =
        [
            0,
            0x11, 0, 0, 0, 1, 0, 0, 0,
            0x22, 0, 0, 0, 1, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0,
        ];
        Assert.True(ItemComposingFrames.TryReadRequest(payload, out var request));
        Assert.Equal(0, request.Kind);
        Assert.Equal(0x1_0000_0011UL, request.BaseWireUid);
        Assert.Equal(0x1_0000_0022UL, request.MaterialWireUid);
        Assert.Equal(0UL, request.SupportWireUid);
        Assert.False(ItemComposingFrames.TryReadRequest(payload.AsSpan(0, 24), out _));
    }

    [Fact]
    public void OpenRefuseAndCloseCarryU16Fields()
    {
        Assert.Equal(OpenedEquipment, ItemComposingFrames.Opened(0));
        Assert.Equal(RefusedCatalyst, ItemComposingFrames.Refused(0, 0x0E));
        Assert.True(ItemComposingFrames.TryReadClose(ClosedSeven, out var value));
        Assert.Equal(ClosedSeven, ItemComposingFrames.Closed(value));
    }

    [Fact]
    public void ResultIsKindMaterialSupportFlagBaseUidTwoPropertiesAndMoney()
    {
        var material = new BagOperation(QuestResultFrames.Removed, 2, 87003, 0x1_0000_0022, 0);
        var flame = new BagOperation(QuestResultFrames.StackChanged, 1, 19689, 0x1_0000_0033, 4);
        var frame = ItemComposingFrames.Result(0, material, flame, 0x1_0000_0011,
            new ItemProperty(12, 26000, 0, 87003), new ItemProperty(13, 26001, 0, 0x1011), 123456);
        var materialBytes = QuestResultFrames.ItemOperation(material);
        var flameBytes = QuestResultFrames.ItemOperation(flame);
        var at = 2;
        Assert.Equal(OpenedEquipment, frame[..2]);
        Assert.Equal(materialBytes, frame[at..(at += materialBytes.Length)]);
        Assert.Equal(1, frame[at++]);
        Assert.Equal(flameBytes, frame[at..(at += flameBytes.Length)]);
        Assert.Equal(0x1_0000_0011UL, BitConverter.ToUInt64(frame, at));
        at += 8;
        Assert.Equal(PropertiesAndMoney, frame[at..]);

        var noSupport = ItemComposingFrames.Result(0, material, null, 0x1_0000_0011,
            new ItemProperty(12, 26000, 0, 87003), new ItemProperty(13, 26001, 0, 0x1011), 123456);
        Assert.Equal(0, noSupport[2 + materialBytes.Length]);
        Assert.Equal(frame.Length - flameBytes.Length, noSupport.Length);
    }

    [Fact]
    public void TheDecomposeResultListsTheKeptPropertiesInNineBytesEach()
    {
        var catalyst = new BagOperation(QuestResultFrames.Removed, 1, 19689, 0x10, 0);
        var skin = new BagOperation(QuestResultFrames.Created, 2, 87003, 0x20, 1, 1, 0, 0, 0);
        var kept = new[] { new tPoseidon.Core.Data.ItemProperty(10, 10000, 7, 3) };
        var frame = ItemComposingFrames.DecomposeResult(0x30, catalyst, skin, kept);
        Assert.Equal(0x30UL, BitConverter.ToUInt64(frame, 0));
        Assert.Equal(new byte[] { 1, 10, 0x10, 0x27, 7, 0, 3, 0, 0, 0 }, frame[^10..]);
        Assert.True(ItemComposingFrames.TryReadDecompose(new byte[16], out _));
        Assert.False(ItemComposingFrames.TryReadDecompose(new byte[15], out _));
    }
}
