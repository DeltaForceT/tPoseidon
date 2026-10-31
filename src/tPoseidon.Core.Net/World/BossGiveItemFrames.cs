namespace tPoseidon.Core.Net.World;

public static class BossGiveItemFrames
{
    public const ushort GivenOpcode = 0x0256;

    public static byte[] Given(ushort bossEntityId, uint itemId, ushort receiverEntityId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt16(bossEntityId);
        w.WriteUInt32(itemId);
        w.WriteUInt16(receiverEntityId);
        return w.ToArray();
    }
}
