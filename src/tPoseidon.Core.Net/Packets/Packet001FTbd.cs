using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet001FTbd
{
    public const int PayloadSize = 17;

    public const ushort ConstAt02 = 0x0001;

    public required ushort CasterEntityId { get; init; }
    public required ushort TargetEntityId { get; init; }
    public required ushort CasterX { get; init; }
    public required ushort CasterY { get; init; }
    public required ushort TargetX { get; init; }
    public required ushort TargetY { get; init; }
    public required ushort AnimSubOp { get; init; }
    public required byte ResultFlag { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), CasterEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), ConstAt02);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), CasterX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(8, 2), CasterY);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), TargetX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(12, 2), TargetY);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(14, 2), AnimSubOp);
        buf[16] = ResultFlag;
        return buf;
    }

    public static Packet001FTbd DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Packet001FTbd payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new Packet001FTbd
        {
            CasterEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            CasterX = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
            CasterY = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..10]),
            TargetX = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]),
            TargetY = BinaryPrimitives.ReadUInt16LittleEndian(payload[12..14]),
            AnimSubOp = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..16]),
            ResultFlag = payload[16],
        };
    }
}
