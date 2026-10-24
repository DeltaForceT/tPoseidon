using tPoseidon.Core.Game.Pets;

namespace tPoseidon.Core.Net.World;

public static class PetLevelUpFrames
{
    public const int RequestSize = 2 * sizeof(ulong);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out PetLevelUpRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new PetLevelUpRequest(r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static byte[] Result(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(1 + operations.Count * 24);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static byte[] Refused(ushort code)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        return w.ToArray();
    }
}
