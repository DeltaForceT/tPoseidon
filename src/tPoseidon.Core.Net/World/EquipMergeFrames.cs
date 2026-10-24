using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class EquipMergeFrames
{
    public const ushort Opened = 0x04F9;
    public const ushort Merge = 0x04F5;
    public const ushort Merged = 0x04F6;
    public const ushort Refused = 0x04F7;
    public const ushort Close = 0x04FB;
    public const ushort Closed = 0x04FC;
    public const int RequestSize = 3 * sizeof(ulong);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out EquipMergeRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new EquipMergeRequest(r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static byte[] Result(bool success, uint money, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(8 + operations.Count * 24);
        w.WriteByte(success ? (byte)1 : (byte)0);
        w.WriteUInt32(money);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static byte[] Code(byte code)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        return w.ToArray();
    }
}
