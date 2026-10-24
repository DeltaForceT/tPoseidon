using System.Text;

namespace tPoseidon.Core.Net.World;

public static class ItemGiveFrames
{
    public const int MaxRecipients = 0x137;
    public static readonly ushort[] OxMaps = [0x2F, 0xDA];
    public const byte RefusedReceiverBusy = 2;
    public const byte RefusedReceiverGone = 3;
    public const byte RefusedNoRoom = 7;

    public static byte[] Refused(uint itemId, ulong itemWireId, ushort count, string receiverName, byte code, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(receiverName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteUInt32(itemId);
        w.WriteUInt64(itemWireId);
        w.WriteUInt16(count);
        w.WriteNulTerminated(receiverName, text);
        w.WriteByte(code);
        return w.ToArray();
    }

    public static byte[] Given(BagOperation giverOperation, ushort count, string receiverName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(receiverName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteBytes(QuestResultFrames.ItemOperation(giverOperation));
        w.WriteUInt16(count);
        w.WriteNulTerminated(receiverName, text);
        return w.ToArray();
    }

    public static byte[] Received(BagOperation receiverOperation, ushort count, ushort giverObjectId, string giverName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(giverName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteBytes(QuestResultFrames.ItemOperation(receiverOperation));
        w.WriteUInt16(count);
        w.WriteUInt16(giverObjectId);
        w.WriteNulTerminated(giverName, text);
        return w.ToArray();
    }
}
