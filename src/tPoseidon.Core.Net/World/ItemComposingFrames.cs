using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public static class ItemComposingFrames
{
    public const int RequestSize = 1 + 3 * sizeof(ulong);
    public const int CloseSize = sizeof(ushort);
    public const ushort DecomposeOpened = 0x04F0;
    public const ushort Decompose = 0x04F1;
    public const ushort Decomposed = 0x04F2;
    public const ushort DecomposeRefused = 0x04F3;
    public const int DecomposeRequestSize = 2 * sizeof(ulong);

    public static byte[] Opened(byte kind)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(kind);
        return w.ToArray();
    }

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out ComposingRequest request)
    {
        request = default;
        if (payload.Length < RequestSize) return false;
        var r = new PacketReader(payload);
        request = new ComposingRequest(r.ReadByte(), r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static bool TryReadClose(ReadOnlySpan<byte> payload, out ushort value)
    {
        value = 0;
        if (payload.Length < CloseSize) return false;
        var r = new PacketReader(payload);
        value = r.ReadUInt16();
        return true;
    }

    public static byte[] Closed(ushort value)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(value);
        return w.ToArray();
    }

    public static byte[] Refused(byte kind, byte code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(kind);
        w.WriteUInt16(code);
        return w.ToArray();
    }

    public static byte[] Result(byte kind, BagOperation material, BagOperation? support, ulong baseWireUid, ItemProperty appearance, ItemProperty options, uint money)
    {
        var w = new PacketWriter(80);
        w.WriteUInt16(kind);
        w.WriteBytes(QuestResultFrames.ItemOperation(material));
        w.WriteByte(support is null ? (byte)0 : (byte)1);
        if (support is { } consumed) w.WriteBytes(QuestResultFrames.ItemOperation(consumed));
        w.WriteUInt64(baseWireUid);
        WriteProperty(w, appearance);
        WriteProperty(w, options);
        w.WriteUInt32(money);
        return w.ToArray();
    }

    public static bool TryReadDecompose(ReadOnlySpan<byte> payload, out DecomposeRequest request)
    {
        request = default;
        if (payload.Length < DecomposeRequestSize) return false;
        var r = new PacketReader(payload);
        request = new DecomposeRequest(r.ReadUInt64(), r.ReadUInt64());
        return true;
    }

    public static byte[] DecomposeCode(byte code)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        return w.ToArray();
    }

    public static byte[] DecomposeResult(ulong itemWireUid, BagOperation catalyst, BagOperation skin, IReadOnlyList<ItemProperty> kept)
    {
        ArgumentNullException.ThrowIfNull(kept);
        var w = new PacketWriter(64 + kept.Count * 9);
        w.WriteUInt64(itemWireUid);
        w.WriteBytes(QuestResultFrames.ItemOperation(catalyst));
        w.WriteBytes(QuestResultFrames.ItemOperation(skin));
        w.WriteByte((byte)kept.Count);
        foreach (var property in kept)
        {
            w.WriteByte((byte)property.Type);
            w.WriteUInt16((ushort)property.Id);
            w.WriteUInt16((ushort)property.Value);
            w.WriteUInt32(property.Extra);
        }
        return w.ToArray();
    }

    private static void WriteProperty(PacketWriter w, ItemProperty property)
    {
        w.WriteByte((byte)property.Type);
        w.WriteUInt16((ushort)property.Id);
        w.WriteUInt32(property.Extra);
    }
}
