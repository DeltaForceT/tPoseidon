namespace tPoseidon.Core.Net.World;

public static class EscortFrames
{
    public const ushort Started = 0;
    public const ushort Failed = 1;
    public const ushort Succeeded = 2;

    public static byte[] Text(ushort index)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(index);
        return w.ToArray();
    }
}
