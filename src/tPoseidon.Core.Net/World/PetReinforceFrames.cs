using System.Buffers.Binary;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.World;

public readonly record struct PetReinforceRequest(ulong PetWireUid, ulong MaterialWireUid, ulong FirstAidWireUid, ulong SecondAidWireUid);

public readonly record struct PetReinforceResetRequest(ulong PetWireUid, ulong MaterialWireUid, uint Slot);

public static class PetReinforceFrames
{
    public const int RequestSize = 32;
    public const int ResetRequestSize = 20;

    public static bool TryReadResetRequest(ReadOnlySpan<byte> payload, out PetReinforceResetRequest request)
    {
        request = default;
        if (payload.Length < ResetRequestSize) return false;
        request = new PetReinforceResetRequest(
            BinaryPrimitives.ReadUInt64LittleEndian(payload),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]));
        return true;
    }

    public static byte[] ResetResult(ulong petWireUid, uint slot, BagOperation material)
    {
        var w = new PacketWriter(40);
        w.WriteUInt64(petWireUid);
        w.WriteUInt32(slot);
        w.WriteBytes(QuestResultFrames.ItemOperation(material));
        return w.ToArray();
    }

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out PetReinforceRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        request = new PetReinforceRequest(
            BinaryPrimitives.ReadUInt64LittleEndian(payload),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[8..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[16..]),
            BinaryPrimitives.ReadUInt64LittleEndian(payload[24..]));
        return true;
    }

    public static byte[] Result(BagOperation material, ulong petWireUid, ItemProperty element, IReadOnlyList<BagOperation> aids)
    {
        ArgumentNullException.ThrowIfNull(aids);
        var w = new PacketWriter(96);
        w.WriteBytes(QuestResultFrames.ItemOperation(material));
        w.WriteUInt64(petWireUid);
        GrowthService.WriteProp9(w, element);
        foreach (var aid in aids) w.WriteBytes(QuestResultFrames.ItemOperation(aid));
        return w.ToArray();
    }
}
