using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Pets;

namespace tPoseidon.Core.Net.World;

public static class PetRetrainFrames
{
    public const int RequestSize = 3 * sizeof(ulong) + sizeof(uint);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out PetRetrainRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new PetRetrainRequest(r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64(), r.ReadInt32());
        return true;
    }

    public static byte[] Result(ulong petWireUid, IReadOnlyList<ItemProperty> stats, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(16 + stats.Count * 4 + operations.Count * QuestResultFrames.RawOperationSize);
        w.WriteUInt64(petWireUid);
        w.WriteByte((byte)stats.Count);
        foreach (var stat in stats)
        {
            w.WriteUInt16((ushort)stat.Id);
            w.WriteInt16(stat.Value);
        }
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) QuestResultFrames.WriteRawOperation(w, operation);
        return w.ToArray();
    }

    public static byte[] Refused(ushort code)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        return w.ToArray();
    }
}
