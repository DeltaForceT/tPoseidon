using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public readonly record struct SpokenLine(ushort EntityId, string Name, string Text);

public readonly record struct SpokenHead(ushort EntityId, string Text);

public static class ChatFrames
{
    public const ushort Say = 0x0036;
    public const ushort Said = 0x0039;
    public const ushort ChatBar = 0x01A1;
    public const ushort ChatBarSaid = 0x01A1;
    public const ushort RateLimited = 0x02C9;
    public const ushort RateLimitedSaid = 0x02C8;
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

    public static byte[] Text(string text, Encoding encoding)
    {
        var w = new PacketWriter(64);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static SpokenLine ReadLine(ReadOnlySpan<byte> body, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var entityId = EntityOf(body);
        var strings = body[sizeof(ushort)..];
        var nameEnd = strings.IndexOf((byte)0);
        if (nameEnd < 0 || strings[(nameEnd + 1)..].IndexOf((byte)0) < 0)
        {
            throw new InvalidPacketException("A chat line needs a NUL-terminated name and text.");
        }
        return new SpokenLine(entityId, encoding.GetString(strings[..nameEnd]), ReadText(strings[(nameEnd + 1)..], encoding));
    }

    public static SpokenHead ReadHead(ReadOnlySpan<byte> body, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var entityId = EntityOf(body);
        var text = body[sizeof(ushort)..];
        if (text.IndexOf((byte)0) < 0) throw new InvalidPacketException("A head message needs a NUL-terminated text.");
        return new SpokenHead(entityId, ReadText(text, encoding));
    }

    private static ushort EntityOf(ReadOnlySpan<byte> body)
    {
        if (body.Length < sizeof(ushort)) throw new InvalidPacketException($"A chat frame needs an entity id, got {body.Length} bytes.");
        return BinaryPrimitives.ReadUInt16LittleEndian(body);
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
