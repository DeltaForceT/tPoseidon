using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class SecretMapFrames
{
    public const int CountSize = sizeof(uint);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out IReadOnlyList<ulong> itemUids)
    {
        itemUids = [];
        if (payload.Length < CountSize) return false;
        var r = new PacketReader(payload);
        var count = r.ReadUInt32();
        if (count > SecretMapRules.CardCount || payload.Length < CountSize + count * sizeof(ulong)) return false;
        var uids = new List<ulong>((int)count);
        for (var i = 0; i < count; i++) uids.Add(r.ReadUInt64());
        itemUids = uids;
        return true;
    }

    public static byte[] Made(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(operations.Count * 24);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }
}
