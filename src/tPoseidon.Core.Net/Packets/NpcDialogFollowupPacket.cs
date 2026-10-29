using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_NPC_DIALOG_FOLLOWUP)]
public sealed class NpcDialogFollowupPacket
{
        public const int TrailerPadBytes = 3;

    public ushort EntityId { get; init; }
    public ushort PageId { get; init; }
    public string Text { get; init; } = string.Empty;
        public ushort BoundQuestId { get; init; }

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(Text);
        var textBytes = ClientText.Encoding.GetBytes(Text);

        var payload = new byte[4 + textBytes.Length + TrailerPadBytes + 2];
        payload[0] = (byte)(EntityId & 0xFF);
        payload[1] = (byte)((EntityId >> 8) & 0xFF);
        payload[2] = (byte)(PageId & 0xFF);
        payload[3] = (byte)((PageId >> 8) & 0xFF);
        Array.Copy(textBytes, 0, payload, 4, textBytes.Length);

        var trailerOffset = 4 + textBytes.Length + TrailerPadBytes;
        payload[trailerOffset] = (byte)(BoundQuestId & 0xFF);
        payload[trailerOffset + 1] = (byte)((BoundQuestId >> 8) & 0xFF);
        return payload;
    }

    public static NpcDialogFollowupPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4 + TrailerPadBytes + 2)
        {
            throw new InvalidPacketException(
                $"NpcDialogFollowup payload must be at least {4 + TrailerPadBytes + 2} bytes, got {payload.Length}.");
        }
        var entityId = (ushort)(payload[0] | (payload[1] << 8));
        var pageId = (ushort)(payload[2] | (payload[3] << 8));
        var textLen = payload.Length - 4 - TrailerPadBytes - 2;
        var text = textLen > 0
            ? ClientText.Encoding.GetString(payload.Slice(4, textLen))
            : string.Empty;
        var trailerOffset = payload.Length - 2;
        var boundQuestId = (ushort)(payload[trailerOffset] | (payload[trailerOffset + 1] << 8));
        return new NpcDialogFollowupPacket
        {
            EntityId = entityId,
            PageId = pageId,
            Text = text,
            BoundQuestId = boundQuestId,
        };
    }
}
