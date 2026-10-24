using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net;
using Xunit;

namespace tPoseidon.Core.Net.Tests;

public sealed class PacketFramerTests
{
    [Fact]
    public void EmptyInput_YieldsNoFrames()
    {
        var framer = new PacketFramer();
        var frames = framer.Feed(ReadOnlySpan<byte>.Empty);
        Assert.Empty(frames);
    }

    [Fact]
    public void SingleWholePacket_InOneFeed_YieldsOneFrame()
    {
        var packet = MakePacket([0xAA, 0xBB, 0xCC]);
        var framer = new PacketFramer();

        var frames = framer.Feed(packet);

        Assert.Single(frames);
        Assert.Equal(packet, frames[0]);
    }

    [Fact]
    public void PacketSplitAcrossTwoFeeds_YieldsFrameOnlyWhenComplete()
    {
        var packet = MakePacket([0x01, 0x02, 0x03, 0x04]);
        var framer = new PacketFramer();

        var first = framer.Feed(packet.AsSpan(0, 3));
        Assert.Empty(first);
        Assert.Equal(3, framer.BufferedBytes);

        var second = framer.Feed(packet.AsSpan(3));
        Assert.Single(second);
        Assert.Equal(packet, second[0]);
        Assert.Equal(0, framer.BufferedBytes);
    }

    [Fact]
    public void PacketSplitByteByByte_YieldsExactlyOneFrame()
    {
        var packet = MakePacket([0x10, 0x20, 0x30, 0x40, 0x50]);
        var framer = new PacketFramer();

        for (var i = 0; i < packet.Length - 1; i++)
        {
            var frames = framer.Feed(packet.AsSpan(i, 1));
            Assert.Empty(frames);
        }

        var last = framer.Feed(packet.AsSpan(packet.Length - 1, 1));
        Assert.Single(last);
        Assert.Equal(packet, last[0]);
    }

    [Fact]
    public void TwoWholePacketsInOneFeed_YieldsBothInOrder()
    {
        var a = MakePacket([0xAA]);
        var b = MakePacket([0xBB, 0xCC]);
        var framer = new PacketFramer();

        var combined = a.Concat(b).ToArray();
        var frames = framer.Feed(combined);

        Assert.Equal(2, frames.Count);
        Assert.Equal(a, frames[0]);
        Assert.Equal(b, frames[1]);
    }

    [Fact]
    public void OneAndAHalfPackets_YieldsOnlyTheFirst_ThenRestOnNextFeed()
    {
        var a = MakePacket([0x01, 0x02]);
        var b = MakePacket([0x10, 0x20, 0x30]);
        var framer = new PacketFramer();

        var combined = a.Concat(b).ToArray();
        var splitPoint = a.Length + 2;

        var first = framer.Feed(combined.AsSpan(0, splitPoint));
        Assert.Single(first);
        Assert.Equal(a, first[0]);

        var second = framer.Feed(combined.AsSpan(splitPoint));
        Assert.Single(second);
        Assert.Equal(b, second[0]);
    }

    [Fact]
    public void ManyBackToBackPackets_InSingleFeed_AllReturnedInOrder()
    {
        var framer = new PacketFramer();
        var packets = new List<byte[]>();
        var combined = new List<byte>();

        for (var i = 0; i < 50; i++)
        {
            var payload = new byte[(i % 7) + 1];
            for (var j = 0; j < payload.Length; j++)
            {
                payload[j] = (byte)(i * 13 + j);
            }

            var packet = MakePacket(payload);
            packets.Add(packet);
            combined.AddRange(packet);
        }

        var frames = framer.Feed(combined.ToArray());

        Assert.Equal(packets.Count, frames.Count);
        for (var i = 0; i < packets.Count; i++)
        {
            Assert.Equal(packets[i], frames[i]);
        }
    }

    [Fact]
    public void LengthBelowMinimum_Throws()
    {
        var framer = new PacketFramer();
        var bogus = new byte[] { 0x01, 0x00 };

        Assert.Throws<InvalidPacketException>(() => framer.Feed(bogus));
    }

    [Fact]
    public void LengthAboveMaximum_Throws()
    {
        var framer = new PacketFramer(maxPacketBytes: 100);
        var bogus = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bogus, 200);

        Assert.Throws<InvalidPacketException>(() => framer.Feed(bogus));
    }

    [Fact]
    public void MaximumSizePacket_AtBoundary_IsAccepted()
    {
        var framer = new PacketFramer(maxPacketBytes: 10);
        var packet = MakePacket(new byte[8]);

        var frames = framer.Feed(packet);

        Assert.Single(frames);
        Assert.Equal(10, frames[0].Length);
    }

    [Fact]
    public void ConstructorRejectsOutOfRangeMaxPacketBytes()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketFramer(maxPacketBytes: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketFramer(maxPacketBytes: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketFramer(maxPacketBytes: 65536));
    }

    [Fact]
    public void LargePacketStreamedInSmallChunks_ReassemblesCorrectly()
    {
        var payload = new byte[4000];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(i % 251);
        }

        var packet = MakePacket(payload);
        var framer = new PacketFramer();

        const int chunk = 37;
        List<byte[]>? collected = null;
        for (var offset = 0; offset < packet.Length; offset += chunk)
        {
            var size = Math.Min(chunk, packet.Length - offset);
            var frames = framer.Feed(packet.AsSpan(offset, size));
            if (frames.Count > 0)
            {
                collected = [.. frames];
            }
        }

        Assert.NotNull(collected);
        Assert.Single(collected!);
        Assert.Equal(packet, collected![0]);
    }

    private static byte[] MakePacket(ReadOnlySpan<byte> payload)
    {
        var total = checked((ushort)(payload.Length + 2));
        var buffer = new byte[total];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, total);
        payload.CopyTo(buffer.AsSpan(2));
        return buffer;
    }
}
