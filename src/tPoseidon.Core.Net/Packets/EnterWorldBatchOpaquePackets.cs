using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class GameBatchHeaderPacket
{
    public const int PayloadSize = 10;

    public ushort Field0Tbd { get; init; } = 0xB397;
    public ushort Field2Tbd { get; init; } = 0x00FF;
    public ushort EntityIdMirror { get; init; } = 0x2E7E;
    public ushort SpawnXTbd { get; init; } = 0x03B5;
    public ushort SpawnYTbd { get; init; } = 0x08B4;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        WriteU16(buf, 0, Field0Tbd);
        WriteU16(buf, 2, Field2Tbd);
        WriteU16(buf, 4, EntityIdMirror);
        WriteU16(buf, 6, SpawnXTbd);
        WriteU16(buf, 8, SpawnYTbd);
        return buf;
    }

    public static GameBatchHeaderPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GameBatchHeader payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new GameBatchHeaderPacket
        {
            Field0Tbd = ReadU16(payload, 0),
            Field2Tbd = ReadU16(payload, 2),
            EntityIdMirror = ReadU16(payload, 4),
            SpawnXTbd = ReadU16(payload, 6),
            SpawnYTbd = ReadU16(payload, 8),
        };
    }

    private static void WriteU16(byte[] buf, int o, ushort v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
    }

    private static ushort ReadU16(ReadOnlySpan<byte> buf, int o) =>
        (ushort)(buf[o] | (buf[o + 1] << 8));
}

public sealed class GameItemExpiryPacket
{
    public const int PayloadSize = 10;

    public ulong Timestamp { get; init; } = 0x01DCD4C66012E880UL;
    public ushort Trailer { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        for (var i = 0; i < 8; i++) buf[i] = (byte)((Timestamp >> (i * 8)) & 0xFF);
        buf[8] = (byte)(Trailer & 0xFF);
        buf[9] = (byte)((Trailer >> 8) & 0xFF);
        return buf;
    }

    public static GameItemExpiryPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"GameItemExpiry payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        ulong ts = 0;
        for (var i = 0; i < 8; i++) ts |= (ulong)payload[i] << (i * 8);
        return new GameItemExpiryPacket
        {
            Timestamp = ts,
            Trailer = (ushort)(payload[8] | (payload[9] << 8)),
        };
    }
}

public sealed class GameBatchFloatArrayPacket
{
    public const int FloatCount2010 = 8;
    public const int FloatCount2014 = 10;
    public const int BonusRateSlot = 2;
    public const int GameMasterRateSlot = 5;

    public float[] Floats { get; init; } = new float[FloatCount2010];

    public static GameBatchFloatArrayPacket ForBonusRateKr2014(float bonusRate)
    {
        var floats = new float[FloatCount2014];
        floats[BonusRateSlot] = bonusRate;
        return new GameBatchFloatArrayPacket { Floats = floats };
    }

    public static GameBatchFloatArrayPacket ForRateSlotsKr2014(float bonusRate, float gameMasterRate)
    {
        var packet = ForBonusRateKr2014(bonusRate);
        packet.Floats[GameMasterRateSlot] = gameMasterRate;
        return packet;
    }

    public byte[] EncodePayload()
    {
        if (Floats is null || (Floats.Length != FloatCount2010 && Floats.Length != FloatCount2014))
        {
            throw new InvalidOperationException(
                $"Floats must have {FloatCount2010} or {FloatCount2014} entries, got {Floats?.Length ?? 0}.");
        }
        var buf = new byte[Floats.Length * 4];
        for (var i = 0; i < Floats.Length; i++)
        {
            var bits = BitConverter.SingleToUInt32Bits(Floats[i]);
            var off = i * 4;
            buf[off] = (byte)(bits & 0xFF);
            buf[off + 1] = (byte)((bits >> 8) & 0xFF);
            buf[off + 2] = (byte)((bits >> 16) & 0xFF);
            buf[off + 3] = (byte)((bits >> 24) & 0xFF);
        }
        return buf;
    }

    public static GameBatchFloatArrayPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != FloatCount2010 * 4 && payload.Length != FloatCount2014 * 4)
        {
            throw new InvalidPacketException(
                $"GameBatchFloatArray payload must be {FloatCount2010 * 4} or {FloatCount2014 * 4} bytes, got {payload.Length}.");
        }
        var count = payload.Length / 4;
        var floats = new float[count];
        for (var i = 0; i < count; i++)
        {
            var off = i * 4;
            uint bits = (uint)(payload[off] | (payload[off + 1] << 8) | (payload[off + 2] << 16) | (payload[off + 3] << 24));
            floats[i] = BitConverter.UInt32BitsToSingle(bits);
        }
        return new GameBatchFloatArrayPacket { Floats = floats };
    }
}

public sealed class SlotCountPacket
{
    public const int PayloadSize = 4;

    public uint Count { get; init; } = 3;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(Count & 0xFF);
        buf[1] = (byte)((Count >> 8) & 0xFF);
        buf[2] = (byte)((Count >> 16) & 0xFF);
        buf[3] = (byte)((Count >> 24) & 0xFF);
        return buf;
    }

    public static SlotCountPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"SlotCount payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new SlotCountPacket
        {
            Count = (uint)(payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24)),
        };
    }
}

public sealed class HeadingVectorPacket
{
    public const int PayloadSize = 12;

    public ushort Heading { get; init; }
    public ushort Pitch { get; init; }
    public ushort Roll { get; init; }
    public ushort Reserved6 { get; init; }
    public ushort Reserved8 { get; init; }
    public ushort Reserved10 { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        WriteU16(buf, 0, Heading);
        WriteU16(buf, 2, Pitch);
        WriteU16(buf, 4, Roll);
        WriteU16(buf, 6, Reserved6);
        WriteU16(buf, 8, Reserved8);
        WriteU16(buf, 10, Reserved10);
        return buf;
    }

    public static HeadingVectorPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"HeadingVector payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new HeadingVectorPacket
        {
            Heading = ReadU16(payload, 0),
            Pitch = ReadU16(payload, 2),
            Roll = ReadU16(payload, 4),
            Reserved6 = ReadU16(payload, 6),
            Reserved8 = ReadU16(payload, 8),
            Reserved10 = ReadU16(payload, 10),
        };
    }

    private static void WriteU16(byte[] buf, int o, ushort v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
    }
    private static ushort ReadU16(ReadOnlySpan<byte> buf, int o) =>
        (ushort)(buf[o] | (buf[o + 1] << 8));
}

public sealed class ColorOrFlagPacket0325
{
    public const int PayloadSize = 4;

    public byte ByteR { get; init; } = 0x3E;
    public byte ByteG { get; init; } = 0xBA;
    public byte ByteB { get; init; } = 0xFF;
    public byte ByteATbd { get; init; }

    public byte[] EncodePayload() => new[] { ByteR, ByteG, ByteB, ByteATbd };

    public static ColorOrFlagPacket0325 DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"ColorOrFlag0325 payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new ColorOrFlagPacket0325
        {
            ByteR = payload[0],
            ByteG = payload[1],
            ByteB = payload[2],
            ByteATbd = payload[3],
        };
    }
}

public sealed class TimestampedSparsePacket0477
{
    public const int PayloadSize = 20;

    public uint TimestampLow { get; init; } = 0x69ED20EDu;

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[12] = (byte)(TimestampLow & 0xFF);
        buf[13] = (byte)((TimestampLow >> 8) & 0xFF);
        buf[14] = (byte)((TimestampLow >> 16) & 0xFF);
        buf[15] = (byte)((TimestampLow >> 24) & 0xFF);
        return buf;
    }

    public static TimestampedSparsePacket0477 DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"TimestampedSparse0477 payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new TimestampedSparsePacket0477
        {
            TimestampLow = (uint)(payload[12] | (payload[13] << 8) | (payload[14] << 16) | (payload[15] << 24)),
        };
    }
}

public sealed class MapHandoffAckPacket
{
    public const int PayloadSize = 4;

    public uint Status { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(Status & 0xFF);
        buf[1] = (byte)((Status >> 8) & 0xFF);
        buf[2] = (byte)((Status >> 16) & 0xFF);
        buf[3] = (byte)((Status >> 24) & 0xFF);
        return buf;
    }

    public static MapHandoffAckPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"MapHandoffAck payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new MapHandoffAckPacket
        {
            Status = (uint)(payload[0] | (payload[1] << 8) | (payload[2] << 16) | (payload[3] << 24)),
        };
    }
}
