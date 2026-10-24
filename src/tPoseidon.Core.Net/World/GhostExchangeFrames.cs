using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class GhostExchangeFrames
{
    public const int StoneRequestSize = 14;
    public const int NeoRequestSize = 12;

    public static bool TryReadStone(ReadOnlySpan<byte> payload, out GhostStoneRequest request)
    {
        request = default;
        if (payload.Length < StoneRequestSize) return false;
        var r = new PacketReader(payload);
        request = new GhostStoneRequest(r.ReadUInt64(), r.ReadUInt16(), r.ReadUInt32());
        return true;
    }

    public static bool TryReadNeo(ReadOnlySpan<byte> payload, out GhostNeoRequest request)
    {
        request = default;
        if (payload.Length < NeoRequestSize) return false;
        var r = new PacketReader(payload);
        request = new GhostNeoRequest(r.ReadUInt64(), r.ReadUInt32());
        return true;
    }

    public static byte[] Exchanged(IReadOnlyList<BagOperation> operations) => QuestResultFrames.ItemOperations(operations);

    public static byte[] Refused(byte code) => [code];
}
