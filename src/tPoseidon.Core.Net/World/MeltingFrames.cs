namespace tPoseidon.Core.Net.World;

public static class MeltingFrames
{
    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out IReadOnlyList<ulong> wireUids)
    {
        wireUids = Array.Empty<ulong>();
        if (payload.Length < 1) return false;
        var r = new PacketReader(payload);
        var count = r.ReadByte();
        if (payload.Length < 1 + count * sizeof(ulong)) return false;
        var uids = new ulong[count];
        for (var i = 0; i < count; i++) uids[i] = r.ReadUInt64();
        wireUids = uids;
        return true;
    }

    public static byte[] Result(uint money, IReadOnlyList<BagOperation> operations) => SlotExtendFrames.MoneyAndOperations(money, operations);

    public static byte[] Refused(byte code) => [code];
}
