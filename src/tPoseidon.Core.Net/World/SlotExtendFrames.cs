using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class SlotExtendFrames
{
    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out SlotExtendRequest request)
    {
        request = default;
        if (payload.Length < 1) return false;
        var r = new PacketReader(payload);
        var count = r.ReadByte();
        if (count < SlotExtendRules.FixedUids || payload.Length < 1 + count * sizeof(ulong)) return false;
        var equipment = r.ReadUInt64();
        var material = r.ReadUInt64();
        var subs = new ulong[count - SlotExtendRules.FixedUids];
        for (var i = 0; i < subs.Length; i++) subs[i] = r.ReadUInt64();
        request = new SlotExtendRequest(equipment, material, subs);
        return true;
    }

    public static byte[] Result(long money, IReadOnlyList<BagOperation> operations) => MoneyAndOperations(money, operations);

    public static byte[] Refused(byte code) => [code];

    public static byte[] MoneyAndOperations(long money, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(6 + operations.Count * 24);
        w.WriteUInt32Field(money);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        return w.ToArray();
    }
}
