using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

public sealed class StatAllocatePacket
{
    public const int PayloadSize = 24;

    public ushort Ap { get; init; }
    public ushort Ac { get; init; }
    public ushort Dx { get; init; }
    public ushort MaxMp { get; init; }
    public ushort Ma { get; init; }
    public ushort Md { get; init; }
    public ushort MaxWt { get; init; }
    public ushort Da { get; init; }
    public ushort Lk { get; init; }
    public ushort MaxHp { get; init; }
    public ushort Dp { get; init; }
    public ushort Hv { get; init; }

    public byte[] EncodePayload()
    {
        var w = new PacketWriter(PayloadSize);
        w.WriteUInt16(Ap);
        w.WriteUInt16(Ac);
        w.WriteUInt16(Dx);
        w.WriteUInt16(MaxMp);
        w.WriteUInt16(Ma);
        w.WriteUInt16(Md);
        w.WriteUInt16(MaxWt);
        w.WriteUInt16(Da);
        w.WriteUInt16(Lk);
        w.WriteUInt16(MaxHp);
        w.WriteUInt16(Dp);
        w.WriteUInt16(Hv);
        return w.ToArray();
    }

    public static StatAllocatePacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"StatAllocate payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        return new StatAllocatePacket
        {
            Ap = r.ReadUInt16(),
            Ac = r.ReadUInt16(),
            Dx = r.ReadUInt16(),
            MaxMp = r.ReadUInt16(),
            Ma = r.ReadUInt16(),
            Md = r.ReadUInt16(),
            MaxWt = r.ReadUInt16(),
            Da = r.ReadUInt16(),
            Lk = r.ReadUInt16(),
            MaxHp = r.ReadUInt16(),
            Dp = r.ReadUInt16(),
            Hv = r.ReadUInt16(),
        };
    }

    public int Sum() => Ap + Ac + Dx + MaxMp + Ma + Md + MaxWt + Da + Lk + MaxHp + Dp + Hv;
}
