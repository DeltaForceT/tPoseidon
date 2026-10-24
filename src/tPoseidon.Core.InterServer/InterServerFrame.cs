using tPoseidon.Core.Common;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class InterServerFrame
{
    private const int LengthPrefixSize = 2;
    private const int OpcodeSize = 2;

    public required InterServerOpcode Opcode { get; init; }
    public required InterServerHeader Header { get; init; }
    public byte[] Body { get; init; } = Array.Empty<byte>();

    public byte[] Encode()
    {
        var bodyLen = Body.Length;
        var total = LengthPrefixSize + OpcodeSize + InterServerHeader.Size + bodyLen;
        if (total > ushort.MaxValue)
        {
            throw new InvalidPacketException(
                $"InterServer frame is {total} bytes; exceeds u16 length prefix max {ushort.MaxValue}.");
        }

        var writer = new PacketWriter(total);
        writer.WriteUInt16((ushort)total);
        writer.WriteUInt16((ushort)Opcode);
        Header.WriteTo(writer);
        writer.WriteBytes(Body);
        return writer.ToArray();
    }

    public static InterServerFrame Decode(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < LengthPrefixSize + OpcodeSize + InterServerHeader.Size)
        {
            throw new InvalidPacketException(
                $"InterServer frame is {frame.Length} bytes; minimum is {LengthPrefixSize + OpcodeSize + InterServerHeader.Size}.");
        }

        var reader = new PacketReader(frame);
        var declaredLength = reader.ReadUInt16();
        if (declaredLength != frame.Length)
        {
            throw new InvalidPacketException(
                $"InterServer frame declared length {declaredLength} does not match actual {frame.Length}.");
        }

        var opcode = (InterServerOpcode)reader.ReadUInt16();
        var header = InterServerHeader.ReadFrom(ref reader);
        var body = reader.Remaining > 0 ? reader.ReadBytes(reader.Remaining) : Array.Empty<byte>();

        return new InterServerFrame
        {
            Opcode = opcode,
            Header = header,
            Body = body,
        };
    }
}
