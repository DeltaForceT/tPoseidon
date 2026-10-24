using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_OP_2E29_TBD)]
public sealed class AngelPairingStatusPacket
{
    public const int PayloadSize = 3;

    public byte PairedFlag { get; init; }
    public ushort PartnerEntityId { get; init; }

    public static readonly AngelPairingStatusPacket NoPairing = new();

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = PairedFlag;
        buf[1] = (byte)(PartnerEntityId & 0xFF);
        buf[2] = (byte)((PartnerEntityId >> 8) & 0xFF);
        return buf;
    }

    public static AngelPairingStatusPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"AngelPairingStatus payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new AngelPairingStatusPacket
        {
            PairedFlag = payload[0],
            PartnerEntityId = (ushort)(payload[1] | (payload[2] << 8)),
        };
    }
}
