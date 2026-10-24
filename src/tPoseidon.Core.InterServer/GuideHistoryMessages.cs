using tPoseidon.Core.Common;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class GuideHistoryLoadAckOk
{
    public required string SystemGuide { get; init; }
    public required ulong TutorialGuide { get; init; }

    public byte[] EncodeBody()
    {
        var name = System.Text.Encoding.UTF8.GetBytes(SystemGuide);
        if (name.Length > ushort.MaxValue)
        {
            throw new InvalidPacketException(
                $"GuideHistoryLoadAckOk.SystemGuide is {name.Length} bytes; exceeds u16 length prefix max.");
        }
        var writer = new PacketWriter(2 + name.Length + 8);
        writer.WriteUInt16((ushort)name.Length);
        writer.WriteBytes(name);
        writer.WriteUInt64(TutorialGuide);
        return writer.ToArray();
    }

    public static GuideHistoryLoadAckOk DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var len = reader.ReadUInt16();
        var name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(len));
        var tutorial = reader.ReadUInt64();
        return new GuideHistoryLoadAckOk
        {
            SystemGuide = name,
            TutorialGuide = tutorial,
        };
    }
}

public sealed class GuideHistoryUpsertSystemRequest
{
    public required string SystemGuide { get; init; }

    public byte[] EncodeBody()
    {
        var name = System.Text.Encoding.UTF8.GetBytes(SystemGuide);
        if (name.Length > 1000)
        {
            throw new InvalidPacketException(
                $"GuideHistoryUpsertSystemRequest.SystemGuide is {name.Length} bytes; schema column is varchar(1000).");
        }
        var writer = new PacketWriter(2 + name.Length);
        writer.WriteUInt16((ushort)name.Length);
        writer.WriteBytes(name);
        return writer.ToArray();
    }

    public static GuideHistoryUpsertSystemRequest DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var len = reader.ReadUInt16();
        var name = System.Text.Encoding.UTF8.GetString(reader.ReadBytes(len));
        return new GuideHistoryUpsertSystemRequest
        {
            SystemGuide = name,
        };
    }
}

public sealed class GuideHistoryUpsertTutorialRequest
{
    public required ulong TutorialGuide { get; init; }

    public byte[] EncodeBody()
    {
        var writer = new PacketWriter(8);
        writer.WriteUInt64(TutorialGuide);
        return writer.ToArray();
    }

    public static GuideHistoryUpsertTutorialRequest DecodeBody(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return new GuideHistoryUpsertTutorialRequest
        {
            TutorialGuide = reader.ReadUInt64(),
        };
    }
}
