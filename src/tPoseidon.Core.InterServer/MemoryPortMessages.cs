using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record MemoryPortSlotRequest(byte PortType, ushort Slot);

public static class MemoryPortMessages
{
    public static byte[] EncodeType(byte portType) => [portType];

    public static byte DecodeType(ReadOnlySpan<byte> body) => new PacketReader(body).ReadByte();

    public static byte[] EncodeSlotRequest(MemoryPortSlotRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(3);
        w.WriteByte(request.PortType);
        w.WriteUInt16(request.Slot);
        return w.ToArray();
    }

    public static MemoryPortSlotRequest DecodeSlotRequest(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new MemoryPortSlotRequest(r.ReadByte(), r.ReadUInt16());
    }

    public static byte[] EncodeSlot(MemoryPort slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        var w = new PacketWriter(64);
        WriteSlot(w, slot);
        return w.ToArray();
    }

    public static MemoryPort DecodeSlot(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return ReadSlot(ref r, characterId);
    }

    public static byte[] EncodeSlots(IReadOnlyList<MemoryPort> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var w = new PacketWriter(1 + slots.Count * 48);
        w.WriteByte((byte)slots.Count);
        foreach (var slot in slots) WriteSlot(w, slot);
        return w.ToArray();
    }

    public static IReadOnlyList<MemoryPort> DecodeSlots(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var count = r.ReadByte();
        var slots = new MemoryPort[count];
        for (var i = 0; i < count; i++) slots[i] = ReadSlot(ref r, characterId);
        return slots;
    }

    public static byte[] EncodeCode(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static uint DecodeCode(ReadOnlySpan<byte> body) => body.Length < sizeof(uint) ? 0 : new PacketReader(body).ReadUInt32();

    private static void WriteSlot(PacketWriter w, MemoryPort slot)
    {
        w.WriteByte(slot.PortType);
        w.WriteUInt16(slot.SlotNum);
        w.WriteInt16(slot.Zone);
        w.WriteInt32(slot.PositionX);
        w.WriteInt32(slot.PositionY);
        w.WriteString(slot.Description ?? string.Empty);
    }

    private static MemoryPort ReadSlot(ref PacketReader r, ulong characterId) => new()
    {
        CharacterId = characterId,
        PortType = r.ReadByte(),
        SlotNum = (byte)r.ReadUInt16(),
        Zone = r.ReadInt16(),
        PositionX = r.ReadInt32(),
        PositionY = r.ReadInt32(),
        Description = r.ReadString(),
    };
}
