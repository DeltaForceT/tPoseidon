using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class AggroNotifyPacket
{
    public const int PayloadSize = 4;

    public required ushort MobEntityId { get; init; }
    public required ushort AttackerEntityId { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), MobEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), AttackerEntityId);
        return buf;
    }

    public static AggroNotifyPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"AggroNotify payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new AggroNotifyPacket
        {
            MobEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            AttackerEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
        };
    }
}
