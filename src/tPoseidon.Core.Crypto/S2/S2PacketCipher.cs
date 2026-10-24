using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Crypto.S2;

public sealed class S2PacketCipher
{
    public const int HeaderSize = 9;
    public const int PayloadChecksumSize = 2;
    public const int MinimumFrameSize = HeaderSize + PayloadChecksumSize;

    public const int OffsetLength = 0;
    public const int OffsetOpcode = 2;
    public const int OffsetSequence = 4;
    public const int OffsetRandKey = 6;
    public const int OffsetPackingFlags = 7;
    public const int OffsetHeaderChecksum = 8;

    public const int PaddingModulus = 13;

    private readonly KeyTable _table;
    private readonly IS2CipherDerivation _derivation;

    public S2PacketCipher(KeyTable table, IS2CipherDerivation derivation)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(derivation);
        _table = table;
        _derivation = derivation;
    }

        public byte[] Encrypt(
        ushort opcode,
        ushort sequence,
        byte randKey,
        PackingFlags flags,
        ReadOnlySpan<byte> plaintextPayload,
        SessionInfo session,
        int dummyPaddingBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentOutOfRangeException.ThrowIfNegative(dummyPaddingBytes);
        if ((flags & PackingFlags.DummyPadding) == 0 && dummyPaddingBytes != 0)
        {
            throw new ArgumentException(
                "DummyPadding flag must be set when dummyPaddingBytes > 0.",
                nameof(flags));
        }

        var totalLength = HeaderSize + plaintextPayload.Length + PayloadChecksumSize + dummyPaddingBytes;
        if (totalLength > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"Frame would be {totalLength} bytes; max u16 is {ushort.MaxValue}.",
                nameof(plaintextPayload));
        }

        var frame = new byte[totalLength];

        plaintextPayload.CopyTo(frame.AsSpan(HeaderSize, plaintextPayload.Length));

        ushort checksum = 0;
        foreach (var b in plaintextPayload) checksum += b;
        var checksumOff = HeaderSize + plaintextPayload.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(checksumOff, 2), checksum);






        if ((flags & PackingFlags.PayloadEncrypted) != 0)
        {
            _derivation.EncryptPayload(
                frame.AsSpan(HeaderSize, plaintextPayload.Length),
                opcode, randKey, _table);
            _derivation.EncryptTrailingChecksum(
                frame.AsSpan(checksumOff, PayloadChecksumSize),
                plaintextPayload.Length, opcode, randKey, _table);
        }




        lock (session.Lock)
        {
            _derivation.EncryptHeader(
                frame, (ushort)totalLength, opcode, sequence, randKey, flags, session.SendKey, _table);

            if ((flags & PackingFlags.KeyUpdate) != 0)
            {
                session.SendKey = _derivation.UpdateSessionKey(session.SendKey, checksum, _table);
            }
        }

        return frame;
    }

        public S2DecryptedPacket Decrypt(ReadOnlySpan<byte> frame, SessionInfo session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (session.Lock)
        {
            return DecryptLocked(frame, session);
        }
    }

    private S2DecryptedPacket DecryptLocked(ReadOnlySpan<byte> frame, SessionInfo session)
    {
        if (frame.Length < MinimumFrameSize)
        {
            throw new InvalidPacketException(
                $"Frame too short: {frame.Length} bytes; needs at least {MinimumFrameSize}.");
        }







        byte packingFlagsByte;
        try
        {
            packingFlagsByte = _derivation.ValidatePackingFlags(frame, _table);
        }
        catch (InvalidPacketException) when (!session.IsStrict && !session.IsFirstPacket)
        {



            packingFlagsByte = frame[OffsetPackingFlags];
        }
        var flags = (PackingFlags)packingFlagsByte;

        var randKey = frame[OffsetRandKey];


        var length = _derivation.RecoverLength(frame, session.RecvKey, _table);
        var opcode = _derivation.RecoverOpcode(frame, session.RecvKey, _table);
        var sequence = _derivation.RecoverSequence(frame, session.RecvKey, _table);

        var adjustedLength = length;
        if ((flags & PackingFlags.DummyPadding) != 0)
        {
            adjustedLength -= (ushort)(randKey % PaddingModulus);
            if (adjustedLength < MinimumFrameSize)
            {
                throw new InvalidPacketException(
                    $"Adjusted length {adjustedLength} below minimum after dummy removal.");
            }
        }

        if (adjustedLength > frame.Length)
        {
            throw new InvalidPacketException(
                $"Decrypted length {adjustedLength} exceeds buffer {frame.Length}.");
        }

        var payloadEnd = adjustedLength - PayloadChecksumSize;
        if (payloadEnd < HeaderSize)
        {
            throw new InvalidPacketException(
                $"Payload region ends before it begins: payloadEnd={payloadEnd}.");
        }

        var rawPayload = frame.Slice(HeaderSize, payloadEnd - HeaderSize);
        var payload = new byte[rawPayload.Length];
        rawPayload.CopyTo(payload);

        if ((flags & PackingFlags.PayloadEncrypted) != 0)
        {
            _derivation.DecryptPayload(payload, opcode, randKey, _table);
        }

        ushort computedChecksum = 0;
        foreach (var b in payload)
        {
            computedChecksum = (ushort)(computedChecksum + b);
        }


        var trailerBytes = new byte[PayloadChecksumSize];
        frame.Slice(payloadEnd, PayloadChecksumSize).CopyTo(trailerBytes);
        if ((flags & PackingFlags.PayloadEncrypted) != 0)
        {
            _derivation.DecryptTrailingChecksum(trailerBytes, payload.Length, opcode, randKey, _table);
        }

        var checksumOnWire = BinaryPrimitives.ReadUInt16LittleEndian(trailerBytes);
        if (computedChecksum != checksumOnWire)
        {
            throw new InvalidPacketException(
                $"Payload checksum mismatch: wire=0x{checksumOnWire:X4} computed=0x{computedChecksum:X4}.");
        }

        if ((flags & PackingFlags.KeyUpdate) != 0)
        {
            session.RecvKey = _derivation.UpdateSessionKey(session.RecvKey, computedChecksum, _table);
        }

        session.IsFirstPacket = false;

        return new S2DecryptedPacket(length, opcode, sequence, flags, randKey, payload);
    }
}

public readonly record struct S2DecryptedPacket(
    ushort DeclaredLength,
    ushort Opcode,
    ushort SequenceNumber,
    PackingFlags Flags,
    byte RandKey,
    byte[] Payload);
