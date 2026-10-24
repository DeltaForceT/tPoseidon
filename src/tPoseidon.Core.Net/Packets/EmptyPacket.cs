namespace tPoseidon.Core.Net.Packets;

public static class EmptyPacket
{
    public static byte[] EncodePayload() => Array.Empty<byte>();
}
