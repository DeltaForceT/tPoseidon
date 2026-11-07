using System.Text;

namespace tPoseidon.Core.Net.Social;

public static class ChatFrames
{
    public const ushort Say = 0x0036;
    public const ushort Said = 0x0039;
    public const ushort ChatBar = 0x01A1;
    public const ushort ChatBarSaid = 0x01A1;
    public const ushort GhostSay = 0x02C9;
    public const ushort GhostSaid = 0x02C8;
    public const ushort HeadMessage = 0x00B6;
    public const ushort HeadMessageShown = 0x00B6;
    public const ushort FindPlayer = 0x0076;
    public const ushort PlayerFound = 0x0077;
    public const ushort PlayerNotFound = 0x0078;
    public const ushort CountPlayers = 0x007A;
    public const ushort PlayerCount = 0x007B;

    public const int HeadMessageLength = 50;

    public static string ReadText(ReadOnlySpan<byte> body, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var end = body.IndexOf((byte)0);
        return encoding.GetString(end < 0 ? body : body[..end]);
    }

    public static byte[] Line(ushort entityId, string name, string text, Encoding encoding)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16(entityId);
        w.WriteNulTerminated(name, encoding);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] Head(ushort entityId, string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(text);
        var kept = bytes.Length <= HeadMessageLength ? bytes : bytes[..HeadMessageLength];
        var w = new PacketWriter(3 + kept.Length);
        w.WriteUInt16(entityId);
        w.WriteBytes(kept);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] Name(string name, Encoding encoding)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, encoding);
        return w.ToArray();
    }

    public static byte[] Count(ushort total)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(total);
        return w.ToArray();
    }
}
