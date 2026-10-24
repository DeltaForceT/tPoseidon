using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.CHANNEL_HANDOFF_REPLY)]
public sealed class ChannelHandoffReplyPacket
{
    public const int CharUidOffset = 0;
    public const int IndexRecordOffset = 8;
    public const int IndexRecordBytes = 6;
    public const int IpOffset = IndexRecordOffset + IndexRecordBytes;
    public const int PortBytes = sizeof(uint);
    public const int BlockMinutesCount = 7;
    public const int BlockMinutesBytes = BlockMinutesCount * sizeof(ushort);
    public const int MinimumPayloadSize = IpOffset + 1 + PortBytes + BlockMinutesBytes;

        public static readonly byte[] SnapshotBytes =
    {
        0x8E, 0x8F, 0x09, 0x06, 0x00, 0x00, 0x00, 0x00,
        0x06, 0x00, 0xDB, 0x0B, 0x00, 0x00,
        0x31, 0x32, 0x37, 0x2E, 0x30, 0x2E, 0x30, 0x2E,
        0x31, 0x00,
        0xDE, 0x59, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    };

    public static ChannelHandoffReplyPacket SnapshotDefault => DecodePayload(SnapshotBytes);

    public ulong SelectedCharUid { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public ushort GamePort { get; init; }

    public ushort[] BlockMinutes { get; init; } = new ushort[BlockMinutesCount];

        public byte[] BaseBytes { get; init; } = Array.Empty<byte>();

    public static int PortOffset(int ipBytes) => IpOffset + ipBytes + 1;

    public static int BlockMinutesOffset(int ipBytes) => PortOffset(ipBytes) + PortBytes;

    public byte[] EncodePayload()
    {
        if (BaseBytes.Length < IpOffset)
        {
            throw new InvalidOperationException(
                $"ChannelHandoffReply.BaseBytes must hold at least the first {IpOffset} bytes, got {BaseBytes.Length}.");
        }
        if (BlockMinutes.Length != BlockMinutesCount)
        {
            throw new InvalidOperationException(
                $"BlockMinutes must hold {BlockMinutesCount} values, got {BlockMinutes.Length}.");
        }
        var ipBytes = Encoding.ASCII.GetBytes(IpAddress);
        if (Array.IndexOf(ipBytes, (byte)0) >= 0)
        {
            throw new InvalidOperationException("IpAddress must not contain a NUL byte.");
        }

        var output = new byte[MinimumPayloadSize + ipBytes.Length];
        BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(CharUidOffset), SelectedCharUid);
        BaseBytes.AsSpan(IndexRecordOffset, IndexRecordBytes).CopyTo(output.AsSpan(IndexRecordOffset));
        ipBytes.CopyTo(output.AsSpan(IpOffset));
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(PortOffset(ipBytes.Length)), GamePort);
        var blocks = BlockMinutesOffset(ipBytes.Length);
        for (var i = 0; i < BlockMinutesCount; i++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(blocks + i * sizeof(ushort)), BlockMinutes[i]);
        }
        return output;
    }

    public static ChannelHandoffReplyPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < MinimumPayloadSize)
        {
            throw new InvalidPacketException(
                $"ChannelHandoffReply payload must be at least {MinimumPayloadSize} bytes, got {payload.Length}.");
        }
        var nul = payload[IpOffset..].IndexOf((byte)0);
        if (nul < 0 || payload.Length != MinimumPayloadSize + nul)
        {
            throw new InvalidPacketException(
                $"ChannelHandoffReply payload of {payload.Length} bytes does not match [u64][6 B][ip NUL][u32 port][{BlockMinutesBytes} B].");
        }
        var port = BinaryPrimitives.ReadUInt32LittleEndian(payload[PortOffset(nul)..]);
        if (port > ushort.MaxValue)
        {
            throw new InvalidPacketException($"ChannelHandoffReply port {port} does not fit a TCP port.");
        }
        var blocks = BlockMinutesOffset(nul);
        var blockMinutes = new ushort[BlockMinutesCount];
        for (var i = 0; i < BlockMinutesCount; i++)
        {
            blockMinutes[i] = BinaryPrimitives.ReadUInt16LittleEndian(payload[(blocks + i * sizeof(ushort))..]);
        }
        return new ChannelHandoffReplyPacket
        {
            SelectedCharUid = BinaryPrimitives.ReadUInt64LittleEndian(payload[CharUidOffset..]),
            IpAddress = Encoding.ASCII.GetString(payload.Slice(IpOffset, nul)),
            GamePort = (ushort)port,
            BlockMinutes = blockMinutes,
            BaseBytes = payload.ToArray(),
        };
    }
}
