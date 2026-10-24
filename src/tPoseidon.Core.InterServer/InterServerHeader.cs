using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public readonly struct InterServerHeader
{
    public const int Size = 2 + 2 + 8 + 2;

    public ushort SourceServerIndex { get; init; }
    public ushort DestServerIndex { get; init; }
    public ulong CharUid { get; init; }
    public ushort RequestId { get; init; }

    public void WriteTo(PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteUInt16(SourceServerIndex);
        writer.WriteUInt16(DestServerIndex);
        writer.WriteUInt64(CharUid);
        writer.WriteUInt16(RequestId);
    }

    public static InterServerHeader ReadFrom(ref PacketReader reader) => new()
    {
        SourceServerIndex = reader.ReadUInt16(),
        DestServerIndex = reader.ReadUInt16(),
        CharUid = reader.ReadUInt64(),
        RequestId = reader.ReadUInt16(),
    };
}
