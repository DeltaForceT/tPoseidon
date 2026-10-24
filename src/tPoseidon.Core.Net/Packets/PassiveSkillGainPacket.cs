using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_OP_2159_TBD)]
public sealed class PassiveSkillGainPacket
{
    public const int PayloadSize = 56;

        public static readonly PassiveSkillGainPacket SnapshotDefault = new()
    {
        Bytes = new byte[]
        {
            0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x3F, 0x3F, 0x00, 0x01,
            0x00, 0x00, 0x00, 0x3F, 0x3F, 0x3F, 0x3F, 0x3F,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x3F, 0x3F, 0x3F,
            0x00, 0x03, 0x00, 0x00, 0x00, 0x3F, 0x3F, 0x3F,
            0x3F, 0x00, 0x04, 0x00, 0x00, 0x00, 0x3F, 0x3F,
            0x3F, 0x3F, 0x3F, 0x00, 0x00, 0x00, 0x00, 0x00,
        },
    };

        public byte[] Bytes { get; init; } = Array.Empty<byte>();

    public byte[] EncodePayload()
    {
        if (Bytes.Length != PayloadSize)
        {
            throw new InvalidOperationException(
                $"PassiveSkillGain.Bytes must be exactly {PayloadSize} bytes, got {Bytes.Length}.");
        }
        var copy = new byte[PayloadSize];
        Array.Copy(Bytes, copy, PayloadSize);
        return copy;
    }

    public static PassiveSkillGainPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"PassiveSkillGain payload must be exactly {PayloadSize} bytes, got {payload.Length}.");
        }
        return new PassiveSkillGainPacket { Bytes = payload.ToArray() };
    }
}
