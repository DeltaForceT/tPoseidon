using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class ItemUseFrames
{
    public const int UseRequestSize = 8;
    public const int UseOnTargetRequestSize = 10;

    public static bool TryReadUseOnTarget(ReadOnlySpan<byte> payload, out ulong wireItemUid, out ushort targetEntityId)
    {
        wireItemUid = 0;
        targetEntityId = 0;
        if (payload.Length < UseOnTargetRequestSize) return false;
        wireItemUid = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        targetEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload[UseRequestSize..]);
        return true;
    }

    public static bool TryReadUse(ReadOnlySpan<byte> payload, out ulong wireItemUid)
    {
        wireItemUid = 0;
        if (payload.Length < UseRequestSize) return false;
        wireItemUid = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        return true;
    }

    public static byte[] Reply(ushort effect, ushort playerEntityId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(effect);
        w.WriteUInt16(playerEntityId);
        return w.ToArray();
    }

    public static byte[] MonsterCardUsed(uint itemId)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(itemId);
        return w.ToArray();
    }

    public static byte[] EffectOnly(ushort effect)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(effect);
        return w.ToArray();
    }

    public static byte[] PresentOpened(uint boxItemId, IReadOnlyList<BagOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var w = new PacketWriter(6 + operations.Count * 32);
        w.WriteUInt32(boxItemId);
        w.WriteUInt16((ushort)operations.Count);
        foreach (var operation in operations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        return w.ToArray();
    }

    public static byte[] PresentRefused()
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(PresentRefusedCode);
        return w.ToArray();
    }

    public const ushort PresentRefusedCode = 1;

    public static byte[] Used(ulong wireItemUid, byte[] reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var w = new PacketWriter(8 + reply.Length);
        w.WriteUInt64(wireItemUid);
        w.WriteBytes(reply);
        return w.ToArray();
    }

    public static byte[] Seen(ushort playerEntityId, uint itemId, byte[] reply)
    {
        ArgumentNullException.ThrowIfNull(reply);
        var w = new PacketWriter(6 + reply.Length);
        w.WriteUInt16(playerEntityId);
        w.WriteUInt32(itemId);
        w.WriteBytes(reply);
        return w.ToArray();
    }

    public static byte[] Notice(string notice, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(32);
        w.WriteNulTerminated(notice, text);
        return w.ToArray();
    }

    public static byte[] Consumed(ulong wireItemUid)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(wireItemUid);
        return w.ToArray();
    }

    public static ulong ReadConsumed(ReadOnlySpan<byte> payload) => ReadItemUid(payload, ConsumedSize);

    public static ulong ReadUsed(ReadOnlySpan<byte> payload) => ReadItemUid(payload, UsedSize);

    private static ulong ReadItemUid(ReadOnlySpan<byte> payload, int size)
    {
        if (payload.Length != size)
        {
            throw new InvalidPacketException($"The item use payload must be exactly {size} bytes, got {payload.Length}.");
        }
        return BinaryPrimitives.ReadUInt64LittleEndian(payload);
    }

    public const int ConsumedSize = sizeof(ulong);
    public const int UsedSize = sizeof(ulong) + ReplySize;
    public const int ReplySize = sizeof(ushort) + sizeof(ushort);
}
