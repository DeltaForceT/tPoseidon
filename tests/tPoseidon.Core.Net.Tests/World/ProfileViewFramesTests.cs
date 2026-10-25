using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ProfileViewFramesTests
{
    private static readonly Encoding Text = Encoding.ASCII;

    private static ProfileViewInfo Info(byte[]? message = null) => new(
        5, 40, 7, 5, "G", new ProfileTitle(2, 0x1122334455667788UL), 3, 4, message ?? [0x41, 0x42], [4, 2, 1, 3]);

    private static ProfileViewEquipment Equipment(IReadOnlyList<ProfileViewWorn> worn) => new(
        Enumerable.Range(1, 12).Select(stat => (uint)stat).ToArray(),
        Enumerable.Range(101, 12).Select(stat => (uint)stat).ToArray(),
        Enumerable.Range(1, 12).Select(stat => (ushort)stat).ToArray(),
        Enumerable.Range(0, 12).Select(stat => (byte)(stat % 4)).ToArray(),
        Enumerable.Range(0, 22).Select(slot => (short)(slot - 3)).ToArray(),
        worn);

    private static ProfileViewWorn Sword() => new(1, new GameItemListPacket.ItemRecord
    {
        Class = 2,
        ItemIndex = 0x1234,
        ItemUid = 0x55,
        EquipmentCount = 1,
        RefineType = 1,
        RefineLevel = 2,
        RefineState = 0,
        MixSlot = 3,
        Options = [new ItemProperty(10, 10000, 7, 0)],
    });

    [Fact]
    public void RequestIsOneEntityId()
    {
        Assert.True(ProfileViewFrames.TryReadRequest([0x34, 0x12], out var entityId));
        Assert.Equal(0x1234, entityId);
        Assert.False(ProfileViewFrames.TryReadRequest([1], out _));
    }

    [Fact]
    public void OptionsAreOneDwordBothWays()
    {
        Assert.True(ProfileViewFrames.TryReadOptions([3, 0, 0, 0], out var flags));
        Assert.Equal(3u, flags);
        Assert.False(ProfileViewFrames.TryReadOptions([3, 0, 0], out _));
        Assert.Equal(new byte[] { 7, 0, 0, 0 }, ProfileViewFrames.Options(7));
    }

    [Fact]
    public void AnUnknownPlayerIsTheDwordOne()
    {
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, ProfileViewFrames.Unknown());
    }

    [Fact]
    public void AViewWithoutTheInfoFlagIsTheHeaderOnly()
    {
        var frame = ProfileViewFrames.View(new ProfileView(0, "Kim", 0x0102, 1, null, null), Text);
        Assert.Equal(new byte[] { 0, 0, 0, 0, (byte)'K', (byte)'i', (byte)'m', 0, 2, 1, 1 }, frame);
    }

    [Fact]
    public void AViewWithTheInfoFlagCarriesTheProfileBlock()
    {
        var frame = ProfileViewFrames.View(new ProfileView(1, "Kim", 0x0102, 1, Info(), null), Text);
        byte[] expected =
        [
            1, 0, 0, 0, (byte)'K', (byte)'i', (byte)'m', 0, 2, 1, 1,
            5, 0, 40, 0, 7, 0, 5, (byte)'G', 0, 2, 1,
            2, 0, 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11,
            3, 0, 4, 0,
            2, 0, 0x41, 0x42,
            4, 0, 2, 0, 1, 0, 3, 0,
        ];
        Assert.Equal(expected, frame);
    }

    [Fact]
    public void AnEmptyMessageIsAZeroLength()
    {
        var frame = ProfileViewFrames.View(new ProfileView(1, "K", 1, 0, Info([]), null), Text);
        var messageAt = frame.Length - 8 - 2;
        Assert.Equal(new byte[] { 0, 0 }, frame[messageAt..(messageAt + 2)]);
    }

    [Fact]
    public void TheEquipmentFlagAddsStatsResistsWornItemsAndTheTypeAndJob()
    {
        var info = ProfileViewFrames.View(new ProfileView(1, "Kim", 0x0102, 1, Info(), null), Text);
        var frame = ProfileViewFrames.View(new ProfileView(3, "Kim", 0x0102, 1, Info(), Equipment([Sword()])), Text);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, frame[..4]);
        Assert.Equal(info[4..], frame[4..info.Length]);
        var tail = frame[info.Length..];
        var at = 0;
        for (var stat = 1; stat <= 12; stat++, at += 4) Assert.Equal(new byte[] { (byte)stat, 0, 0, 0 }, tail[at..(at + 4)]);
        for (var stat = 101; stat <= 112; stat++, at += 4) Assert.Equal(new byte[] { (byte)stat, 0, 0, 0 }, tail[at..(at + 4)]);
        for (var stat = 1; stat <= 12; stat++, at += 2) Assert.Equal(new byte[] { (byte)stat, 0 }, tail[at..(at + 2)]);
        for (var stat = 0; stat < 12; stat++, at++) Assert.Equal((byte)(stat % 4), tail[at]);
        for (var slot = 0; slot < 22; slot++, at += 2) Assert.Equal(BitConverter.GetBytes((short)(slot - 3)), tail[at..(at + 2)]);
        byte[] expectedWorn =
        [
            1,
            1,
            2, 0x34, 0x12, 0, 0, 0x55, 0, 0, 0, 1, 0, 0, 0, 1, 0, 1, 2, 0, 3,
            1, 10, 0, 0, 0, 0x10, 0x27, 7, 0, 0, 0, 0, 0,
            5, 0, 5,
        ];
        Assert.Equal(expectedWorn, tail[at..]);
    }

    [Fact]
    public void TheCollectionFlagAddsAnEmptyList()
    {
        var info = ProfileViewFrames.View(new ProfileView(1, "Kim", 0x0102, 1, Info(), null), Text);
        var frame = ProfileViewFrames.View(new ProfileView(5, "Kim", 0x0102, 1, Info(), null), Text);
        Assert.Equal(info.Length + 1, frame.Length);
        Assert.Equal((byte)0, frame[^1]);
        Assert.Equal(info[4..], frame[4..^1]);
    }

    [Fact]
    public void AFlagWithoutItsBlockIsAnError()
    {
        Assert.Throws<InvalidOperationException>(() => ProfileViewFrames.View(new ProfileView(1, "K", 1, 0, null, null), Text));
        Assert.Throws<InvalidOperationException>(() => ProfileViewFrames.View(new ProfileView(3, "K", 1, 0, Info(), null), Text));
    }

    [Fact]
    public void ABlockOfTheWrongSizeIsAnError()
    {
        var short12 = Equipment([]) with { Totals = [1, 2, 3] };
        Assert.Throws<InvalidOperationException>(() => ProfileViewFrames.View(new ProfileView(3, "K", 1, 0, Info(), short12), Text));
        Assert.Throws<InvalidOperationException>(() => ProfileViewFrames.View(new ProfileView(1, "K", 1, 0, Info() with { GrowthWeights = [1] }, null), Text));
    }
}
