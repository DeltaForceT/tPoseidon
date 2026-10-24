namespace tPoseidon.Core.Crypto.S2;

public interface IS2CipherDerivation
{
        byte ValidatePackingFlags(ReadOnlySpan<byte> frame, KeyTable table);

        ushort RecoverLength(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table);

        ushort RecoverOpcode(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table);

        ushort RecoverSequence(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table);

        void DecryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table);

        void EncryptHeader(
        Span<byte> frame,
        ushort length,
        ushort opcode,
        ushort sequence,
        byte randKey,
        PackingFlags flags,
        byte sessionKey,
        KeyTable table);

        void EncryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table);

        void DecryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table);

        void EncryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table);

        byte UpdateSessionKey(byte oldSessionKey, ushort plaintextChecksum, KeyTable table);
}
