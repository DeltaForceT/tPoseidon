using System.Text;

namespace tPoseidon.Core.Net.World;

public static class HonorTitleFrames
{
    public const int ProfileTitleRequestSize = 10;

    public static byte[] List(IReadOnlyList<ushort> titleIds, byte trailing)
    {
        ArgumentNullException.ThrowIfNull(titleIds);
        var w = new PacketWriter(2 + titleIds.Count * sizeof(ushort));
        w.WriteByte((byte)titleIds.Count);
        foreach (var id in titleIds) w.WriteUInt16(id);
        w.WriteByte(trailing);
        return w.ToArray();
    }

    public static byte[] Acquired(ushort titleId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(titleId);
        return w.ToArray();
    }

    public static bool TryReadProfileTitle(ReadOnlySpan<byte> payload, out ushort titleType, out ulong titleId)
    {
        titleType = 0;
        titleId = 0;
        if (payload.Length < ProfileTitleRequestSize) return false;
        var r = new PacketReader(payload);
        titleType = r.ReadUInt16();
        titleId = r.ReadUInt64();
        return true;
    }

    public static byte[] ProfileTitle(ushort entityId, string partnerName, ushort titleType, ulong titleId, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(partnerName);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(16 + partnerName.Length * 2);
        w.WriteUInt16(entityId);
        w.WriteNulTerminated(partnerName, encoding);
        w.WriteUInt16(titleType);
        w.WriteUInt64(titleId);
        return w.ToArray();
    }
}
