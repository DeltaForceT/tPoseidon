using System.Buffers.Binary;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public static class ExchangeFrames
{
    public static byte[] List(IReadOnlyList<int> offerIds)
    {
        ArgumentNullException.ThrowIfNull(offerIds);
        var w = new PacketWriter(1 + offerIds.Count * 2);
        w.WriteByte((byte)offerIds.Count);
        foreach (var id in offerIds) w.WriteUInt16((ushort)id);
        return w.ToArray();
    }

    public static byte[] Offer(ExchangeOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var w = new PacketWriter(8 + offer.Results.Count * 4 + offer.Requirements.Count * 5);
        w.WriteUInt16((ushort)offer.Id);
        w.WriteUInt32((uint)offer.Fee);
        w.WriteByte((byte)offer.Results.Count);
        foreach (var result in offer.Results) w.WriteUInt32((uint)result);
        w.WriteByte((byte)offer.Requirements.Count);
        foreach (var requirement in offer.Requirements)
        {
            w.WriteUInt32((uint)requirement.ItemId);
            w.WriteByte((byte)requirement.Count);
        }
        return w.ToArray();
    }

    public static byte[] Exchanged(IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(1 + operations.Count * 24);
        w.WriteByte((byte)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static bool TryReadExchange(ReadOnlySpan<byte> payload, out int offerId, out IReadOnlyList<CompoundMaterial> materials, out int times)
    {
        offerId = 0;
        times = 0;
        materials = Array.Empty<CompoundMaterial>();
        if (payload.Length < 3) return false;
        offerId = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        var body = payload[2..];
        var timesAt = 1 + body[0] * CompoundFrames.MaterialSize;
        if (body.Length < timesAt + 1 || !CompoundFrames.TryReadMaterials(body, out materials)) return false;
        times = body[timesAt];
        return true;
    }
}
