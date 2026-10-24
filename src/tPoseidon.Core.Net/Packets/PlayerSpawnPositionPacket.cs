using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.GAME_PLAYER_SPAWN_POSITION)]
public sealed class PlayerSpawnPositionPacket
{
    public const int PositionPayloadSize = 10;
    public const int NoCouplePayloadSize = 14;
    public const int WithCouplePayloadSize = 26;

    public required ushort EntityId { get; init; }

    public ushort PositionX { get; init; }
    public ushort PositionY { get; init; }

    public ulong? CharUid { get; init; }
    public CoupleData? Couple { get; init; }

    public static PlayerSpawnPositionPacket Identity(ushort entityId, ulong charUid, CoupleData? couple = null) =>
        new()
        {
            EntityId = entityId,
            CharUid = charUid,
            Couple = couple,
        };

    public byte[] EncodePayload()
    {
        if (CharUid is null)
        {
            var pos = new byte[PositionPayloadSize];
            BinaryPrimitives.WriteUInt16LittleEndian(pos.AsSpan(0, 2), EntityId);
            BinaryPrimitives.WriteUInt16LittleEndian(pos.AsSpan(2, 2), PositionX);
            BinaryPrimitives.WriteUInt16LittleEndian(pos.AsSpan(4, 2), PositionY);
            BinaryPrimitives.WriteUInt32LittleEndian(pos.AsSpan(6, 4), 0);
            return pos;
        }

        var size = Couple is null ? NoCouplePayloadSize : WithCouplePayloadSize;
        var buf = new byte[size];
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0, 2), EntityId);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(2, 8), CharUid.Value);

        if (Couple is null)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(10, 4), 0);
        }
        else
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(10, 4), Couple.Value.Field0);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(14, 4), Couple.Value.Field1);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(18, 4), Couple.Value.Field2);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(22, 4), Couple.Value.Field3);
        }
        return buf;
    }

    public static PlayerSpawnPositionPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length == PositionPayloadSize)
        {
            return new PlayerSpawnPositionPacket
            {
                EntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]),
                PositionX = BinaryPrimitives.ReadUInt16LittleEndian(payload[2..4]),
                PositionY = BinaryPrimitives.ReadUInt16LittleEndian(payload[4..6]),
            };
        }

        if (payload.Length is NoCouplePayloadSize or WithCouplePayloadSize)
        {
            var entityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[0..2]);
            var charUid = BinaryPrimitives.ReadUInt64LittleEndian(payload[2..10]);

            CoupleData? couple = null;
            if (payload.Length == WithCouplePayloadSize)
            {
                couple = new CoupleData(
                    BinaryPrimitives.ReadUInt32LittleEndian(payload[10..14]),
                    BinaryPrimitives.ReadUInt32LittleEndian(payload[14..18]),
                    BinaryPrimitives.ReadUInt32LittleEndian(payload[18..22]),
                    BinaryPrimitives.ReadUInt32LittleEndian(payload[22..26]));
            }

            return new PlayerSpawnPositionPacket
            {
                EntityId = entityId,
                CharUid = charUid,
                Couple = couple,
            };
        }

        throw new InvalidPacketException(
            $"PlayerSpawnPosition payload must be {PositionPayloadSize}, {NoCouplePayloadSize} or {WithCouplePayloadSize} bytes, got {payload.Length}.");
    }

    public readonly record struct CoupleData(uint Field0, uint Field1, uint Field2, uint Field3);
}
