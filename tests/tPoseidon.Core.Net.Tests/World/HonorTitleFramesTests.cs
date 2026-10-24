using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class HonorTitleFramesTests
{
    [Fact]
    public void ListIsCountIdsAndTheTrailingByte()
    {
        Assert.Equal(new byte[] { 2, 0x09, 0x00, 0x3B, 0x00, 0x00 }, HonorTitleFrames.List(new ushort[] { 9, 59 }, 0));
        Assert.Equal(new byte[] { 0, 0 }, HonorTitleFrames.List(Array.Empty<ushort>(), 0));
    }

    [Fact]
    public void AcquiredIsTheTitleId()
    {
        Assert.Equal(new byte[] { 0x49, 0x00 }, HonorTitleFrames.Acquired(73));
    }

    [Fact]
    public void ProfileTitleRequestIsTypeThenSixtyFourBitId()
    {
        var payload = new byte[] { 7, 0, 9, 0, 0, 0, 0, 0, 0, 0 };
        Assert.True(HonorTitleFrames.TryReadProfileTitle(payload, out var type, out var id));
        Assert.Equal((7, 9UL), (type, id));
        Assert.False(HonorTitleFrames.TryReadProfileTitle(payload.AsSpan(0, 9), out _, out _));
    }

    [Fact]
    public void ProfileTitleBroadcastCarriesEntityNameTypeAndId()
    {
        var frame = HonorTitleFrames.ProfileTitle(0x1234, "Ab", 7, 9, Encoding.ASCII);
        Assert.Equal(new byte[] { 0x34, 0x12, (byte)'A', (byte)'b', 0, 7, 0, 9, 0, 0, 0, 0, 0, 0, 0 }, frame);
        Assert.Equal(13, HonorTitleFrames.ProfileTitle(1, string.Empty, 0, 0, Encoding.ASCII).Length);
    }

    [Fact]
    public void MasterMessagesRoundTrip()
    {
        var add = new HonorTitleAdd(12, 0x4001, new[] { new HonorTitleRecord(9, 1054, 2, 29, 120), new HonorTitleRecord(1, 4065, 1, 0, 0) });
        var encodedAdd = HonorTitleMessages.EncodeAdd(add);
        Assert.Equal(5 + 2 * HonorTitleMessages.EntrySize, encodedAdd.Length);
        var decodedAdd = HonorTitleMessages.DecodeAdd(encodedAdd);
        Assert.Equal((add.ZoneId, add.EntityId), (decodedAdd.ZoneId, decodedAdd.EntityId));
        Assert.Equal(add.Titles, decodedAdd.Titles);

        var added = HonorTitleMessages.DecodeAdded(HonorTitleMessages.EncodeAdded(new HonorTitleAdded(12, 0x4001, new ushort[] { 9, 1 })));
        Assert.Equal(new ushort[] { 9, 1 }, added.TitleIds);

        var login = HonorTitleMessages.DecodeLogin(HonorTitleMessages.EncodeLogin(new HonorTitleLogin(new ushort[] { 3, 4 }, 7, 3)));
        Assert.Equal(new ushort[] { 3, 4 }, login.Titles);
        Assert.Equal((7, 3u), (login.ProfileTitleType, login.ProfileTitleId));

        Assert.Equal((7, 0x1_0000_0002UL), HonorTitleMessages.DecodeProfileTitle(HonorTitleMessages.EncodeProfileTitle(7, 0x1_0000_0002UL)));
    }
}
