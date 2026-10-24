using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class DamageTickPacket
{
    public const int PayloadSize = 26;

    public required ushort VictimEntityId { get; init; }
    public required ushort Damage { get; init; }
    public required byte HitFlags { get; init; }
    public required uint HitKind { get; init; }

    public uint SkillMarker { get; init; }
    public uint ElementBitfield { get; init; }
    public uint CombatFlag { get; init; }
    public uint AttackerClass { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), VictimEntityId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(2, 2), Damage);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(6, 4), SkillMarker);
        buf[10] = HitFlags;
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(14, 4), ElementBitfield);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(18, 4), CombatFlag);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(22, 4), HitKind);
        return buf;
    }

    public static DamageTickPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"DamageTick payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new DamageTickPacket
        {
            VictimEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
            Damage = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
            SkillMarker = BinaryPrimitives.ReadUInt32LittleEndian(payload[6..10]),
            HitFlags = payload[10],
            ElementBitfield = BinaryPrimitives.ReadUInt32LittleEndian(payload[14..18]),
            CombatFlag = BinaryPrimitives.ReadUInt32LittleEndian(payload[18..22]),
            HitKind = BinaryPrimitives.ReadUInt32LittleEndian(payload[22..26]),
        };
    }
}
