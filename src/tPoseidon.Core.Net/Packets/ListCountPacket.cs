namespace tPoseidon.Core.Net.Packets;

public sealed class ListCountPacket
{
    public const int PayloadSize = 2;

    public ushort Count { get; init; }

    public static readonly ListCountPacket Empty = new() { Count = 0 };

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = (byte)(Count & 0xFF);
        buf[1] = (byte)((Count >> 8) & 0xFF);
        return buf;
    }
}
