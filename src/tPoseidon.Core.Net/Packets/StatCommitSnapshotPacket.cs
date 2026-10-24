namespace tPoseidon.Core.Net.Packets;

public sealed class StatCommitSnapshotPacket
{
    public const int PayloadSize = 45;

    public ushort ApAccPts { get; init; }
    public ushort AcAccPts { get; init; }
    public ushort DxAccPts { get; init; }
    public ushort MaxMpAccPts { get; init; }
    public ushort MaAccPts { get; init; }
    public ushort MdAccPts { get; init; }
    public ushort MaxWtAccPts { get; init; }
    public ushort DaAccPts { get; init; }
    public ushort LkAccPts { get; init; }
    public ushort MaxHpAccPts { get; init; }
    public ushort DpAccPts { get; init; }
    public ushort HvAccPts { get; init; }
    public ushort AttrPts { get; init; }

    public ushort UnknownU16At26 { get; init; }
    public ushort TmLevel { get; init; }
    public ushort Level { get; init; }
    public byte UnknownByteAt32 { get; init; }

    public uint MaxHp { get; init; }
    public uint MaxMp { get; init; }
    public uint MaxWt { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        var off = 0;
        WriteU16(buf, ref off, ApAccPts);
        WriteU16(buf, ref off, AcAccPts);
        WriteU16(buf, ref off, DxAccPts);
        WriteU16(buf, ref off, MaxMpAccPts);
        WriteU16(buf, ref off, MaAccPts);
        WriteU16(buf, ref off, MdAccPts);
        WriteU16(buf, ref off, MaxWtAccPts);
        WriteU16(buf, ref off, DaAccPts);
        WriteU16(buf, ref off, LkAccPts);
        WriteU16(buf, ref off, MaxHpAccPts);
        WriteU16(buf, ref off, DpAccPts);
        WriteU16(buf, ref off, HvAccPts);
        WriteU16(buf, ref off, AttrPts);
        WriteU16(buf, ref off, UnknownU16At26);
        WriteU16(buf, ref off, TmLevel);
        WriteU16(buf, ref off, Level);
        buf[off++] = UnknownByteAt32;
        WriteU32(buf, ref off, MaxHp);
        WriteU32(buf, ref off, MaxMp);
        WriteU32(buf, ref off, MaxWt);
        return buf;
    }

    private static void WriteU16(byte[] buf, ref int off, ushort v)
    {
        buf[off++] = (byte)(v & 0xFF);
        buf[off++] = (byte)((v >> 8) & 0xFF);
    }

    private static void WriteU32(byte[] buf, ref int off, uint v)
    {
        buf[off++] = (byte)(v & 0xFF);
        buf[off++] = (byte)((v >> 8) & 0xFF);
        buf[off++] = (byte)((v >> 16) & 0xFF);
        buf[off++] = (byte)((v >> 24) & 0xFF);
    }
}

public sealed class EntityStateBumpPacket
{
    public const int PayloadSize = 4;

    public required ushort EntityId { get; init; }
    public ushort StateSeq { get; init; }

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(EntityId & 0xFF);
        buf[1] = (byte)((EntityId >> 8) & 0xFF);
        buf[2] = (byte)(StateSeq & 0xFF);
        buf[3] = (byte)((StateSeq >> 8) & 0xFF);
        return buf;
    }
}
