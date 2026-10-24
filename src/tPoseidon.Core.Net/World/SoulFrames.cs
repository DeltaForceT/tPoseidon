using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Soul;

namespace tPoseidon.Core.Net.World;

public readonly record struct SoulItemInstance(ulong WireUid, uint ItemId, ItemProperty Property);

public readonly record struct SoulManageStatus(
    uint Soul,
    ulong GuardianUid,
    ushort Gauge,
    ushort GaugeMax,
    ulong SourceWireUid,
    ushort SourceCount,
    uint PotionItemId,
    ushort PotionsReady,
    byte Penalty);

public static class SoulFrames
{
    public const int RegisterSize = sizeof(ulong) + sizeof(ushort);
    public const int TakeSize = sizeof(ushort);
    public const int ChargeSize = sizeof(ulong) + sizeof(uint);
    public const int CraftSize = sizeof(uint) + SoulRules.MaterialSlots * sizeof(ulong);
    public const int ReinforceSize = sizeof(ulong) + SoulRules.MaterialSlots * sizeof(ulong);
    public const int ItemInstanceSize = 22;
    public const uint UnusedStatusField = 0;
    public const byte InstanceTrailer = 0;

    public static bool TryReadRegister(ReadOnlySpan<byte> payload, out ulong sourceWireUid, out ushort count)
    {
        sourceWireUid = 0;
        count = 0;
        if (payload.Length < RegisterSize) return false;
        var r = new PacketReader(payload);
        sourceWireUid = r.ReadUInt64();
        count = r.ReadUInt16();
        return true;
    }

    public static bool TryReadTake(ReadOnlySpan<byte> payload, out ushort count)
    {
        count = 0;
        if (payload.Length < TakeSize) return false;
        count = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static bool TryReadCharge(ReadOnlySpan<byte> payload, out ulong itemWireUid, out uint amount)
    {
        itemWireUid = 0;
        amount = 0;
        if (payload.Length < ChargeSize) return false;
        var r = new PacketReader(payload);
        itemWireUid = r.ReadUInt64();
        amount = r.ReadUInt32();
        return true;
    }

    public static bool TryReadCraft(ReadOnlySpan<byte> payload, out uint resultItemId, out ulong[] materialWireUids)
    {
        resultItemId = 0;
        materialWireUids = [];
        if (payload.Length < CraftSize) return false;
        var r = new PacketReader(payload);
        resultItemId = r.ReadUInt32();
        materialWireUids = ReadUids(ref r);
        return true;
    }

    public static bool TryReadReinforce(ReadOnlySpan<byte> payload, out ulong targetWireUid, out ulong[] materialWireUids)
    {
        targetWireUid = 0;
        materialWireUids = [];
        if (payload.Length < ReinforceSize) return false;
        var r = new PacketReader(payload);
        targetWireUid = r.ReadUInt64();
        materialWireUids = ReadUids(ref r);
        return true;
    }

    public static byte[] Code(byte code) => [code];

    public static byte[] ManageStatus(SoulManageStatus status, BagOperation? potion)
    {
        var w = new PacketWriter(64);
        w.WriteUInt32(status.Soul);
        w.WriteUInt64(status.GuardianUid);
        w.WriteUInt16(status.Gauge);
        w.WriteUInt16(status.GaugeMax);
        w.WriteUInt64(status.SourceWireUid);
        w.WriteUInt32(UnusedStatusField);
        w.WriteUInt16(status.SourceCount);
        w.WriteUInt32(status.PotionItemId);
        w.WriteUInt16(status.PotionsReady);
        w.WriteByte(status.Penalty);
        w.WriteByte(potion is null ? (byte)0 : (byte)1);
        if (potion is { } made) w.WriteBytes(QuestResultFrames.ItemOperation(made));
        return w.ToArray();
    }

    public static byte[] Charged(uint soul, IReadOnlyList<SoulItemInstance> instances)
    {
        var w = new PacketWriter(8 + InstancesSize(instances));
        w.WriteUInt32(soul);
        WriteInstances(w, instances);
        return w.ToArray();
    }

    public static byte[] Crafted(bool ok, uint soul, IReadOnlyList<BagOperation> operations)
    {
        var w = new PacketWriter(32);
        w.WriteByte(ok ? (byte)1 : (byte)0);
        w.WriteUInt32(soul);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        return w.ToArray();
    }

    public static byte[] Reinforced(uint soul, IReadOnlyList<BagOperation> operations, IReadOnlyList<SoulItemInstance> instances)
    {
        var w = new PacketWriter(32 + InstancesSize(instances));
        w.WriteUInt32(soul);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        WriteInstances(w, instances);
        return w.ToArray();
    }

    public static byte[] PotionUsed(uint soul, IReadOnlyList<BagOperation> operations)
    {
        var w = new PacketWriter(32);
        w.WriteUInt32(soul);
        w.WriteBytes(QuestResultFrames.ItemOperations(operations));
        return w.ToArray();
    }

    public static byte[] PropertyUpdates(IReadOnlyList<SoulItemInstance> instances)
    {
        var w = new PacketWriter(2 + InstancesSize(instances));
        WriteInstances(w, instances);
        return w.ToArray();
    }

    private static ulong[] ReadUids(ref PacketReader r)
    {
        var uids = new ulong[SoulRules.MaterialSlots];
        for (var i = 0; i < uids.Length; i++) uids[i] = r.ReadUInt64();
        return uids;
    }

    private static int InstancesSize(IReadOnlyList<SoulItemInstance> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        return 2 + instances.Count * ItemInstanceSize;
    }

    private static void WriteInstances(PacketWriter w, IReadOnlyList<SoulItemInstance> instances)
    {
        w.WriteUInt16((ushort)instances.Count);
        foreach (var instance in instances)
        {
            w.WriteUInt64(instance.WireUid);
            w.WriteUInt32(instance.ItemId);
            w.WriteByte((byte)instance.Property.Type);
            w.WriteUInt16((ushort)instance.Property.Id);
            w.WriteInt16(instance.Property.Value);
            w.WriteUInt32(instance.Property.Extra);
            w.WriteByte(InstanceTrailer);
        }
    }
}
