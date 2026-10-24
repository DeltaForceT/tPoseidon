using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_NPC_DIALOG)]
public sealed class NpcDialogPacket
{
    public ushort EntityId { get; init; }
    public ushort PortraitType { get; init; }
    public byte NpcSType { get; init; }

        public string Text { get; init; } = string.Empty;

        public const int TrailingPadBytes = 5;

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Text);
        var textBytes = Encoding.ASCII.GetBytes(Text);

        var payload = new byte[5 + textBytes.Length + TrailingPadBytes];
        payload[0] = (byte)(EntityId & 0xFF);
        payload[1] = (byte)((EntityId >> 8) & 0xFF);
        payload[2] = (byte)(PortraitType & 0xFF);
        payload[3] = (byte)((PortraitType >> 8) & 0xFF);
        payload[4] = NpcSType;
        Array.Copy(textBytes, 0, payload, 5, textBytes.Length);

        return payload;
    }

    public static NpcDialogPacket DecodePayload(ReadOnlySpan<byte> payload, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(textEncoding);
        var r = new PacketReader(payload);
        var entityId = r.ReadUInt16();
        var portraitType = r.ReadUInt16();
        var npcSType = r.ReadByte();
        var text = npcSType > 0 ? r.ReadNulTerminated(textEncoding) : string.Empty;
        return new NpcDialogPacket
        {
            EntityId = entityId,
            PortraitType = portraitType,
            NpcSType = npcSType,
            Text = text,
        };
    }

    public static NpcDialogPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 5)
        {
            throw new InvalidPacketException(
                $"NpcDialog payload must be at least 5 bytes (header), got {payload.Length}.");
        }
        var entityId = (ushort)(payload[0] | (payload[1] << 8));
        var portraitType = (ushort)(payload[2] | (payload[3] << 8));
        var npcSType = payload[4];




        var text = string.Empty;
        if (payload.Length > 5)
        {
            var textRegion = payload[5..];


            var end = textRegion.Length;
            while (end > 0 && textRegion[end - 1] == 0) end--;
            text = Encoding.ASCII.GetString(textRegion[..end]);
        }
        return new NpcDialogPacket
        {
            EntityId = entityId,
            PortraitType = portraitType,
            NpcSType = npcSType,
            Text = text,
        };
    }
}
