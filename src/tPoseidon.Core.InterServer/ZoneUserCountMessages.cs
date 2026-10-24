using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class ZoneUserCountMessages
{
    public static byte[] EncodeRequest(ushort zone)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static ushort DecodeRequest(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt16();

    public static byte[] EncodeRouted(ushort zone, ushort originRequestId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(zone);
        w.WriteUInt16(originRequestId);
        return w.ToArray();
    }

    public static (ushort Zone, ushort OriginRequestId) DecodeRouted(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt16(), r.ReadUInt16());
    }

    public static byte[] EncodeCount(ushort zone, int count)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(zone);
        w.WriteUInt32((uint)Math.Max(0, count));
        return w.ToArray();
    }

    public static (ushort Zone, int Count) DecodeCount(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt16(), (int)r.ReadUInt32());
    }
}
