using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.Packets;

public sealed class Kr2014CharDeleteRequestPacketTests
{
    private const string EmptyInputDigest = "8f00b204e9800998";

    private static byte[] BuildPayload(ulong uid, string digest)
    {
        var payload = new byte[CharDeleteRequestPacket.PayloadSize];
        BitConverter.TryWriteBytes(payload.AsSpan(0, 8), uid);
        Encoding.ASCII.GetBytes(digest, payload.AsSpan(8));
        return payload;
    }

    [Fact]
    public void DecodePayload_ReadsCharacterUidThenDigest()
    {
        var payload = BuildPayload(0x10001002ul, EmptyInputDigest);

        var typed = CharDeleteRequestPacket.DecodePayload(payload);

        Assert.Equal(0x10001002ul, typed.CharacterUid);
        Assert.Equal(EmptyInputDigest, typed.SecondPasswordDigest);
    }

    [Fact]
    public void DecodePayload_IgnoresBytesAfterTheTerminator()
    {
        var payload = BuildPayload(1, "ab");
        payload[8 + 5] = (byte)'z';

        var typed = CharDeleteRequestPacket.DecodePayload(payload);

        Assert.Equal("ab", typed.SecondPasswordDigest);
    }

    [Fact]
    public void EncodePayload_RoundTrips()
    {
        var source = new CharDeleteRequestPacket { CharacterUid = 0x1122334455667788ul, SecondPasswordDigest = EmptyInputDigest };

        var bytes = source.EncodePayload();
        var back = CharDeleteRequestPacket.DecodePayload(bytes);

        Assert.Equal(BuildPayload(0x1122334455667788ul, EmptyInputDigest), bytes);
        Assert.Equal(0x1122334455667788ul, back.CharacterUid);
        Assert.Equal(EmptyInputDigest, back.SecondPasswordDigest);
    }

    [Fact]
    public void DecodePayload_RejectsTheEightByteForm()
    {
        Assert.Throws<InvalidPacketException>(() => CharDeleteRequestPacket.DecodePayload(new byte[8]));
    }
}
