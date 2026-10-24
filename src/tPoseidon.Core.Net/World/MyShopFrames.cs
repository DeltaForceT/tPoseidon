using System.Text;

namespace tPoseidon.Core.Net.World;

public static class MyShopFrames
{
    public static byte[] EmptyList() => new byte[2];

    public static byte[] Entered(string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(text.Length + 1);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] Leave(ushort returnZone, ushort x, ushort y)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(returnZone);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        return w.ToArray();
    }
}
