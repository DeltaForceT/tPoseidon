using System.Text;

namespace tPoseidon.Core.Net.Packets;

public sealed class Packet01D0Tbd
{
    public ushort EntityId { get; init; }

    public string Speech { get; init; } = string.Empty;

    public byte[] EncodePayload(Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(textEncoding);
        ArgumentNullException.ThrowIfNull(Speech);

        var speechBytes = textEncoding.GetBytes(Speech);
        var total = 2 + speechBytes.Length + 1;

        var w = new PacketWriter(total);
        w.WriteUInt16(EntityId);
        w.WriteBytes(speechBytes);
        w.WriteByte(0);
        return w.ToArray();
    }

}
