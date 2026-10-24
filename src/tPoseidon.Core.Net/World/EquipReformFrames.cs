using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class EquipReformFrames
{
    public const int RequestSize = 4 * sizeof(ulong) + 4 * sizeof(uint);
    public const int ResetRequestSize = 2 * sizeof(ulong);
    public const int PropertyIdBits = 16;

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out EquipReformRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        var target = r.ReadUInt64();
        var material = r.ReadUInt64();
        var firstElixir = r.ReadUInt64();
        var secondElixir = r.ReadUInt64();
        var first = new ReformStatChoice(r.ReadInt32(), r.ReadInt32());
        var second = new ReformStatChoice(r.ReadInt32(), r.ReadInt32());
        request = new EquipReformRequest(target, material, firstElixir, secondElixir, first, second);
        return true;
    }

    public static bool TryReadResetRequest(ReadOnlySpan<byte> payload, out ReformCountResetRequest request)
    {
        request = default;
        if (payload.Length < ResetRequestSize) return false;
        var r = new PacketReader(payload);
        request = new ReformCountResetRequest(r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static byte[] Refused(uint code, uint money)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(code);
        w.WriteUInt32(money);
        return w.ToArray();
    }

    public static byte[] Result(
        BagOperation material,
        BagOperation? elixir,
        ulong targetWireUid,
        uint targetItemId,
        IReadOnlyList<ItemProperty> properties,
        uint money,
        ReformRefineBonus refine)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var w = new PacketWriter(96 + properties.Count * 12);
        QuestResultFrames.WriteRawOperation(w, material);
        w.WriteByte(elixir is null ? (byte)1 : (byte)0);
        if (elixir is { } used) QuestResultFrames.WriteRawOperation(w, used);
        w.WriteUInt64(targetWireUid);
        w.WriteUInt32(targetItemId);
        w.WriteUInt32((uint)properties.Count);
        foreach (var property in properties) WriteProperty(w, property);
        w.WriteUInt32(money);
        w.WriteUInt16((ushort)refine.PropertyId);
        w.WriteUInt16((ushort)refine.Value);
        return w.ToArray();
    }

    public static byte[] ResetDone(ulong targetWireUid, ulong consumedWireUid)
    {
        var w = new PacketWriter(16);
        w.WriteUInt64(targetWireUid);
        w.WriteUInt64(consumedWireUid);
        return w.ToArray();
    }

    public static void WriteProperty(PacketWriter w, ItemProperty property)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteUInt32((uint)property.Type);
        w.WriteUInt32((uint)(ushort)property.Id | ((uint)(ushort)property.Value << PropertyIdBits));
        w.WriteUInt32(property.Extra);
    }
}
