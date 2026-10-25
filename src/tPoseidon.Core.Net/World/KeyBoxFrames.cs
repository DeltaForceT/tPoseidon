using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class KeyBoxFrames
{
    public const int OpenRequestSize = sizeof(ulong);
    public const int ConfirmRequestSize = 2 * sizeof(ulong);

    public static bool TryReadOpen(ReadOnlySpan<byte> payload, out ulong boxUid)
    {
        boxUid = 0;
        if (payload.Length < OpenRequestSize) return false;
        boxUid = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static bool TryReadConfirm(ReadOnlySpan<byte> payload, out ulong boxUid, out ulong keyUid)
    {
        boxUid = 0;
        keyUid = 0;
        if (payload.Length < ConfirmRequestSize) return false;
        var r = new PacketReader(payload);
        boxUid = r.ReadUInt64();
        keyUid = r.ReadUInt64();
        return true;
    }

    public static byte[] OpenRefused() => [KeyBoxRules.OpenRefusedCode];

    public static byte[] ConfirmRefused(KeyBoxRefusal refusal)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16((ushort)refusal);
        return w.ToArray();
    }

    public static byte[] Results(IReadOnlyList<BagOperation> operations) => QuestResultFrames.ItemOperations(operations);
}
