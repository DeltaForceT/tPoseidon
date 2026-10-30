using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public readonly record struct MegaphoneEnqueueRequest(MegaphoneRefund Refund, string Message);

public readonly record struct MegaphonePost(long Code, ushort Kind, string Message);

public static class MegaphoneMessages
{
    public static byte[] EncodeRequest(MegaphoneEnqueueRequest request, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(request.Message);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(8 + request.Message.Length);
        w.WriteInt32(request.Refund.ItemIndex);
        w.WriteByte(request.Refund.ItemClass);
        w.WriteNulTerminated(request.Message, text);
        return w.ToArray();
    }

    public static MegaphoneEnqueueRequest DecodeRequest(ReadOnlySpan<byte> body, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var r = new PacketReader(body);
        var refund = new MegaphoneRefund(r.ReadInt32(), r.ReadByte());
        return new MegaphoneEnqueueRequest(refund, r.ReadNulTerminated(text));
    }

    public static byte[] EncodeValue(uint waiting)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(waiting);
        return w.ToArray();
    }

    public static uint DecodeValue(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt32();

    public static byte[] EncodePost(MegaphonePost post, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(post.Message);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(16 + post.Message.Length);
        w.WriteUInt64(unchecked((ulong)post.Code));
        w.WriteUInt16(post.Kind);
        w.WriteNulTerminated(post.Message, text);
        return w.ToArray();
    }

    public static MegaphonePost DecodePost(ReadOnlySpan<byte> body, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var r = new PacketReader(body);
        return new MegaphonePost(unchecked((long)r.ReadUInt64()), r.ReadUInt16(), r.ReadNulTerminated(text));
    }

    public static byte[] EncodeAck(long code)
    {
        var w = new PacketWriter(sizeof(ulong));
        w.WriteUInt64(unchecked((ulong)code));
        return w.ToArray();
    }

    public static long DecodeAck(ReadOnlySpan<byte> body) => unchecked((long)new PacketReader(body).ReadUInt64());
}
