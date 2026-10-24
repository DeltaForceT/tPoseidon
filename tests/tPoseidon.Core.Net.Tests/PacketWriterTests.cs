using tPoseidon.Core.Common;
using tPoseidon.Core.Net;
using Xunit;

namespace tPoseidon.Core.Net.Tests;

public sealed class PacketWriterTests
{
    [Fact]
    public void WriteAndReadByte_RoundTrips()
    {
        var w = new PacketWriter();
        w.WriteByte(0x42);
        var buf = w.ToArray();
        Assert.Single(buf);
        var r = new PacketReader(buf);
        Assert.Equal(0x42, r.ReadByte());
    }

    [Fact]
    public void WriteAndReadAllPrimitives_RoundTrips()
    {
        var w = new PacketWriter();
        w.WriteByte(0x01);
        w.WriteUInt16(0x0203);
        w.WriteInt16(-2);
        w.WriteUInt32(0x04050607);
        w.WriteInt32(-3);
        w.WriteUInt64(0x0809_0A0B_0C0D_0E0F);
        w.WriteInt64(-4);

        var r = new PacketReader(w.ToArray());
        Assert.Equal(0x01, r.ReadByte());
        Assert.Equal(0x0203, r.ReadUInt16());
        Assert.Equal(-2, r.ReadInt16());
        Assert.Equal(0x04050607u, r.ReadUInt32());
        Assert.Equal(-3, r.ReadInt32());
        Assert.Equal(0x0809_0A0B_0C0D_0E0Ful, r.ReadUInt64());
        Assert.Equal(-4, r.ReadInt64());
    }

    [Fact]
    public void Multibyte_Integers_Are_LittleEndian()
    {
        var w = new PacketWriter();
        w.WriteUInt16(0x0102);
        var buf = w.ToArray();
        Assert.Equal(new byte[] { 0x02, 0x01 }, buf);
    }

    [Fact]
    public void WriteString_PrefixesByteCount()
    {
        var w = new PacketWriter();
        w.WriteString("ABC");
        var buf = w.ToArray();
        Assert.Equal(new byte[] { 0x03, 0x00, (byte)'A', (byte)'B', (byte)'C' }, buf);

        var r = new PacketReader(buf);
        Assert.Equal("ABC", r.ReadString());
    }

    [Fact]
    public void WriteFixedString_ZeroPadsWhenShorter()
    {
        var w = new PacketWriter();
        w.WriteFixedString("Hi", 8);
        var buf = w.ToArray();
        Assert.Equal(new byte[] { (byte)'H', (byte)'i', 0, 0, 0, 0, 0, 0 }, buf);

        var r = new PacketReader(buf);
        Assert.Equal("Hi", r.ReadFixedString(8));
    }

    [Fact]
    public void WriteFixedString_TooLong_Throws()
    {
        var w = new PacketWriter();
        Assert.Throws<ArgumentException>(() => w.WriteFixedString("0123456789", 4));
    }

    [Fact]
    public void WriteBytes_Verbatim()
    {
        var w = new PacketWriter();
        w.WriteBytes(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE });
        Assert.Equal(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE }, w.ToArray());
    }

    [Fact]
    public void WriteZero_PadsWithZeroes()
    {
        var w = new PacketWriter();
        w.WriteByte(0xFF);
        w.WriteZero(3);
        Assert.Equal(new byte[] { 0xFF, 0x00, 0x00, 0x00 }, w.ToArray());
    }

    [Fact]
    public void Buffer_Grows_Geometrically()
    {
        var w = new PacketWriter(initialCapacity: 8);
        var data = new byte[256];
        for (var i = 0; i < data.Length; i++) data[i] = (byte)i;
        w.WriteBytes(data);
        Assert.Equal(256, w.Position);
        Assert.Equal(data, w.ToArray());
    }

    [Fact]
    public void Reset_AllowsReuse()
    {
        var w = new PacketWriter();
        w.WriteUInt16(0x1234);
        Assert.Equal(2, w.Position);
        w.Reset();
        Assert.Equal(0, w.Position);
        w.WriteByte(0xFF);
        Assert.Equal(new byte[] { 0xFF }, w.ToArray());
    }

    [Fact]
    public void Reader_Skip_AdvancesPosition()
    {
        var r = new PacketReader(new byte[] { 1, 2, 3, 4, 5 });
        r.Skip(2);
        Assert.Equal(3, r.ReadByte());
    }

    [Fact]
    public void Reader_ReadPastEnd_Throws()
    {
        Assert.Throws<InvalidPacketException>(() =>
        {
            var r = new PacketReader(new byte[] { 0x00 });
            r.ReadUInt16();
        });
    }

    [Fact]
    public void Reader_FixedString_TrimsTrailingZeroes()
    {
        var r = new PacketReader(new byte[] { (byte)'H', (byte)'i', 0, 0, 0 });
        Assert.Equal("Hi", r.ReadFixedString(5));
    }

    [Fact]
    public void Reader_Remaining_Tracks()
    {
        var r = new PacketReader(new byte[] { 1, 2, 3, 4 });
        Assert.Equal(4, r.Remaining);
        r.ReadUInt16();
        Assert.Equal(2, r.Remaining);
    }
}
