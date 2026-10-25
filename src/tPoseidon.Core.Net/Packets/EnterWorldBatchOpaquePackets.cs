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

    private static void WriteU16(byte[] buf, int o, ushort v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
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

    private static void WriteU16(byte[] buf, int o, ushort v)
    {
        buf[o] = (byte)(v & 0xFF);
        buf[o + 1] = (byte)((v >> 8) & 0xFF);
    }
}

public sealed class ColorOrFlagPacket0325
{
    public byte ByteR { get; init; } = 0x3E;
    public byte ByteG { get; init; } = 0xBA;
    public byte ByteB { get; init; } = 0xFF;
    public byte ByteATbd { get; init; }

    public byte[] EncodePayload() => new[] { ByteR, ByteG, ByteB, ByteATbd };
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
}
