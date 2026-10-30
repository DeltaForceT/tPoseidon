using System.Text;
using tPoseidon.Core.Game.Party;

namespace tPoseidon.Core.Net.Social;

public static class PartyFollowFrames
{
    public static byte[] Started(string targetName, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(targetName);
        ArgumentNullException.ThrowIfNull(textEncoding);
        var w = new PacketWriter(textEncoding.GetByteCount(targetName) + 1);
        w.WriteNulTerminated(targetName, textEncoding);
        return w.ToArray();
    }

    public static byte[] Refused(PartyFollowRefusal code)
    {
        var w = new PacketWriter(sizeof(byte));
        w.WriteByte((byte)code);
        return w.ToArray();
    }

    public static byte[] PortalFollow(ushort portalX, ushort portalY, string leaderName, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(leaderName);
        ArgumentNullException.ThrowIfNull(textEncoding);
        var w = new PacketWriter(sizeof(ushort) * 2 + textEncoding.GetByteCount(leaderName) + 1);
        w.WriteUInt16(portalX);
        w.WriteUInt16(portalY);
        w.WriteNulTerminated(leaderName, textEncoding);
        return w.ToArray();
    }

    public static ushort ReadTargetId(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(ushort))
        {
            throw new tPoseidon.Core.Common.InvalidPacketException($"Party follow request needs {sizeof(ushort)} bytes, got {payload.Length}.");
        }
        return new PacketReader(payload).ReadUInt16();
    }
}
