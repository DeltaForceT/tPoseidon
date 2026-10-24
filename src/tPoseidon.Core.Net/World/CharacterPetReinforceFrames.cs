namespace tPoseidon.Core.Net.World;

public readonly record struct CharacterPetReinforceRequest(ulong ReinforceItemWireUid, ulong PetWireUid);

public static class CharacterPetReinforceFrames
{
    public const int RequestSize = 2 * sizeof(ulong);

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out CharacterPetReinforceRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new CharacterPetReinforceRequest(r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static byte[] Result(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(2 + operations.Count * 24);
        w.WriteUInt16((ushort)operations.Count);
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
