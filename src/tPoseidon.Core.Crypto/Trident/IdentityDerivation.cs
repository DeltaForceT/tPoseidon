using System.Buffers.Binary;

namespace tPoseidon.Core.Crypto.Trident;

public sealed class IdentityDerivation : ITridentCipherDerivation
{
    public byte ValidatePackingFlags(ReadOnlySpan<byte> frame, KeyTable table) =>
        frame[TridentPacketCipher.OffsetPackingFlags];

    public ushort RecoverLength(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table) =>
        BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetLength, 2));

    public ushort RecoverOpcode(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table) =>
        BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetOpcode, 2));

    public ushort RecoverSequence(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table) =>
        BinaryPrimitives.ReadUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetSequence, 2));

    public void DecryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table)
    {
    }

    public void EncryptHeader(
        Span<byte> frame,
        ushort length,
        ushort opcode,
        ushort sequence,
        byte randKey,
        PackingFlags flags,
        byte sessionKey,
        KeyTable table)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetLength, 2), length);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetOpcode, 2), opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.Slice(TridentPacketCipher.OffsetSequence, 2), sequence);
        frame[TridentPacketCipher.OffsetRandKey] = randKey;
        frame[TridentPacketCipher.OffsetPackingFlags] = (byte)flags;
        frame[TridentPacketCipher.OffsetHeaderChecksum] = 0;
    }

    public void EncryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table)
    {
    }

    public void DecryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table)
    {
    }

    public void EncryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table)
    {
    }

    public byte UpdateSessionKey(byte oldSessionKey, ushort plaintextChecksum, KeyTable table)
    {
        var k = oldSessionKey;
        k ^= (byte)(plaintextChecksum & 0xFF);
        k ^= (byte)((plaintextChecksum >> 8) & 0xFF);
        return k;
    }
}
