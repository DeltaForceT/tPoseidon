using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public readonly record struct BagOperation(
    byte Kind,
    byte ItemClass,
    uint ItemId,
    ulong ItemUid,
    ushort Count,
    byte MixSlot = 0,
    byte RefineType = 0,
    byte RefineLevel = 0,
    byte RefineState = 0);

public static class QuestResultFrames
{
    public const byte StackChanged = 1;
    public const byte Moved = 2;
    public const byte Removed = 3;
    public const byte Created = 4;
    public const int RareItemExtraBytes = 4;
    public const byte NoOptions = 0;

    public static byte[] Result(
        IReadOnlyList<BagOperation> operations,
        IReadOnlyList<(uint ItemId, ushort Count)> rewards,
        uint exp,
        uint tmExp,
        uint questId,
        ushort resultRowCount,
        uint gelder)
    {
        var w = new PacketWriter(64);
        WriteOperationsAndRewards(w, operations, rewards);
        w.WriteUInt32(exp);
        w.WriteUInt32(tmExp);
        w.WriteUInt32(questId);
        w.WriteUInt16(resultRowCount);
        w.WriteUInt32(gelder);
        return w.ToArray();
    }

    public static byte[] QuestResult(
        IReadOnlyList<BagOperation> operations,
        IReadOnlyList<(uint ItemId, ushort Count)> rewards,
        uint exp,
        uint tmExp,
        ushort questId,
        ushort count,
        uint gelder,
        ushort rewardSkill,
        ushort rewardSkillLevel)
    {
        var w = new PacketWriter(64);
        WriteOperationsAndRewards(w, operations, rewards);
        w.WriteUInt32(exp);
        w.WriteUInt32(tmExp);
        w.WriteUInt16(questId);
        w.WriteUInt16(count);
        w.WriteUInt32(gelder);
        w.WriteUInt16(rewardSkill);
        w.WriteUInt16(rewardSkillLevel);
        return w.ToArray();
    }

    public static byte[] MonsterQuestResult(
        IReadOnlyList<BagOperation> operations,
        IReadOnlyList<(uint ItemId, ushort Count)> rewards,
        uint exp,
        uint tmExp,
        ushort questId,
        uint gelder)
    {
        var w = new PacketWriter(64);
        WriteOperationsAndRewards(w, operations, rewards);
        w.WriteUInt32(exp);
        w.WriteUInt32(tmExp);
        w.WriteUInt16(questId);
        w.WriteUInt32(gelder);
        return w.ToArray();
    }

    public static byte[] PartyQuestResult(IReadOnlyList<BagOperation> operations, uint exp, uint tmExp, uint questId)
    {
        var w = new PacketWriter(32);
        w.WriteUInt32(exp);
        w.WriteUInt32(tmExp);
        WriteOperations(w, operations);
        w.WriteUInt32(questId);
        return w.ToArray();
    }

    public static byte[] ItemsTaken(IReadOnlyList<BagOperation> operations, IReadOnlyList<(uint ItemId, ushort Count)> required)
    {
        var w = new PacketWriter(32);
        WriteOperationsAndRewards(w, operations, required);
        return w.ToArray();
    }

    public static byte[] ItemOperations(IReadOnlyList<BagOperation> operations)
    {
        var w = new PacketWriter(32);
        WriteOperations(w, operations);
        return w.ToArray();
    }

    private static void WriteOperationsAndRewards(
        PacketWriter w,
        IReadOnlyList<BagOperation> operations,
        IReadOnlyList<(uint ItemId, ushort Count)> rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        WriteOperations(w, operations);
        w.WriteUInt16((ushort)rewards.Count);
        foreach (var (itemId, count) in rewards)
        {
            w.WriteUInt32(itemId);
            w.WriteUInt16(count);
        }
    }

    public const int RawOperationSize = 28;
    public const int RawOperationPadding = 2;

    public static byte[] RawItemOperation(BagOperation operation)
    {
        var w = new PacketWriter(RawOperationSize);
        WriteRawOperation(w, operation);
        return w.ToArray();
    }

    public static void WriteRawOperation(PacketWriter w, BagOperation operation)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteUInt32(operation.Kind);
        w.WriteUInt32(operation.ItemClass);
        w.WriteUInt32(operation.ItemId);
        w.WriteUInt64(operation.ItemUid);
        w.WriteUInt16(operation.Count);
        w.WriteByte(operation.RefineType);
        w.WriteByte(operation.RefineLevel);
        w.WriteByte(operation.RefineState);
        w.WriteByte(operation.MixSlot);
        w.WriteZero(RawOperationPadding);
    }

    public static byte[] ItemOperation(BagOperation operation)
    {
        var w = new PacketWriter(24);
        WriteOperation(w, operation);
        return w.ToArray();
    }

    private static void WriteOperations(PacketWriter w, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        w.WriteUInt16((ushort)operations.Count);
        foreach (var operation in operations) WriteOperation(w, operation);
    }

    private static void WriteOperation(PacketWriter w, BagOperation operation)
    {
        w.WriteByte(operation.Kind);
        w.WriteByte(operation.ItemClass);
        w.WriteUInt32(operation.ItemId);
        w.WriteUInt64(operation.ItemUid);
        w.WriteUInt16(operation.Count);
        if (operation.ItemClass != ItemParamEntry.RareClass) return;
        w.WriteByte(operation.RefineType);
        w.WriteByte(operation.RefineLevel);
        w.WriteByte(operation.RefineState);
        w.WriteByte(operation.MixSlot);
        if (operation.Kind is Moved or Created) w.WriteByte(NoOptions);
    }

    public static BagOperation ReadItemOperation(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var operation = ReadOperation(ref r);
        EnsureEnd(ref r);
        return operation;
    }

    public static IReadOnlyList<BagOperation> ReadItemOperations(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        var operations = ReadOperations(ref r);
        EnsureEnd(ref r);
        return operations;
    }

    public static IReadOnlyList<BagOperation> ReadLeadingOperations(ReadOnlySpan<byte> payload)
    {
        var r = new PacketReader(payload);
        return ReadOperations(ref r);
    }

    public static IReadOnlyList<BagOperation> ReadOperations(ref PacketReader reader)
    {
        var count = reader.ReadUInt16();
        var operations = new List<BagOperation>(count);
        for (var index = 0; index < count; index++) operations.Add(ReadOperation(ref reader));
        return operations;
    }

    public static BagOperation ReadOperation(ref PacketReader reader)
    {
        var kind = reader.ReadByte();
        var itemClass = reader.ReadByte();
        var itemId = reader.ReadUInt32();
        var itemUid = reader.ReadUInt64();
        var count = reader.ReadUInt16();
        if (itemClass != ItemParamEntry.RareClass) return new BagOperation(kind, itemClass, itemId, itemUid, count);
        var refineType = reader.ReadByte();
        var refineLevel = reader.ReadByte();
        var refineState = reader.ReadByte();
        var mixSlot = reader.ReadByte();
        if (kind is Moved or Created)
        {
            var options = reader.ReadByte();
            if (options != NoOptions)
            {
                throw new InvalidPacketException($"The bag operation for item {itemId} lists {options} options; bag operations carry none.");
            }
        }
        return new BagOperation(kind, itemClass, itemId, itemUid, count, mixSlot, refineType, refineLevel, refineState);
    }

    private static void EnsureEnd(ref PacketReader reader)
    {
        if (reader.Remaining != 0)
        {
            throw new InvalidPacketException($"The bag operation frame has {reader.Remaining} bytes after its operations.");
        }
    }
}
