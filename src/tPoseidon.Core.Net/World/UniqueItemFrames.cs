namespace tPoseidon.Core.Net.World;

public static class UniqueItemFrames
{
    public static byte[] DutyExpire(long expireFileTime, long nowFileTime)
    {
        var w = new PacketWriter(16);
        w.WriteInt64(expireFileTime);
        w.WriteInt64(nowFileTime);
        return w.ToArray();
    }

    public static byte[] BossKilled(ushort monsterId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(monsterId);
        return w.ToArray();
    }

    public static byte[] Withdrawn(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(4 + operations.Count * 24);
        w.WriteUInt32((uint)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }
}
