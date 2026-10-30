using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public static class BossFrames
{
    public const int MarkerSize = sizeof(ushort) + sizeof(byte);
    public const int ResistGrownSize = sizeof(ushort) + sizeof(ushort);

    public static byte[] Marker(BossEvent bossEvent)
    {
        var w = new PacketWriter(MarkerSize);
        w.WriteUInt16(bossEvent.EntityId);
        w.WriteByte(bossEvent.NotEarthQuake ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] ResistGrown(BossEvent bossEvent)
    {
        var w = new PacketWriter(ResistGrownSize);
        w.WriteUInt16(bossEvent.EntityId);
        w.WriteUInt16(bossEvent.Element);
        return w.ToArray();
    }

    public static byte[] OwnedEntity(ushort entityId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(entityId);
        return w.ToArray();
    }

    public static byte[] Despawn(ushort entityId)
    {
        var w = new PacketWriter(MarkerSize);
        w.WriteUInt16(entityId);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] Speech(ushort entityId, string text)
    {
        var bytes = ClientText.Encoding.GetBytes(text);
        var w = new PacketWriter(sizeof(ushort) + bytes.Length + sizeof(byte));
        w.WriteUInt16(entityId);
        w.WriteBytes(bytes);
        w.WriteByte(0);
        return w.ToArray();
    }
}
