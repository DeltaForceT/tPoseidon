using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class MobStrikePacket
{
    public const int PayloadSize = 17;
    public const ushort MeleeMarker = 0x0001;

    public required ushort AttackerEntityId { get; init; }
    public required ushort TargetEntityId { get; init; }
    public required ushort AttackerX { get; init; }
    public required ushort AttackerY { get; init; }
    public required ushort TargetX { get; init; }
    public required ushort TargetY { get; init; }
    public required ushort Damage { get; init; }
    public required byte HitKind { get; init; }
    public ushort Marker { get; init; } = MeleeMarker;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), AttackerEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), Marker);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4, 2), TargetEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(6, 2), AttackerX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(8, 2), AttackerY);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(10, 2), TargetX);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(12, 2), TargetY);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(14, 2), Damage);
        buf[16] = HitKind;
        return buf;
    }

    public static MobStrikePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"MobStrike payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new MobStrikePacket
        {
            AttackerEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[..2]),
            Marker = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            TargetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            AttackerX = BinaryPrimitives.ReadUInt16LittleEndian(payload[6..8]),
            AttackerY = BinaryPrimitives.ReadUInt16LittleEndian(payload[8..10]),
            TargetX = BinaryPrimitives.ReadUInt16LittleEndian(payload[10..12]),
            TargetY = BinaryPrimitives.ReadUInt16LittleEndian(payload[12..14]),
            Damage = BinaryPrimitives.ReadUInt16LittleEndian(payload[14..16]),
            HitKind = payload[16],
        };
    }
}
