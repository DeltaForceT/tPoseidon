using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Crypto;

public sealed class PacketCipher
{
    public const int LengthFieldSize = 2;
    public const int CrcFieldSize = 2;
    public const int MinHeaderSize = LengthFieldSize + CrcFieldSize;

    private readonly Crc16 _crc;
    private readonly ISubstitutionTable _encryptTable;
    private readonly ISubstitutionTable _decryptTable;
    private readonly int _headerPaddingBytes;

    public PacketCipher(
        Crc16 crc,
        ISubstitutionTable encryptTable,
        ISubstitutionTable decryptTable,
        int headerPaddingBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(crc);
        ArgumentNullException.ThrowIfNull(encryptTable);
        ArgumentNullException.ThrowIfNull(decryptTable);
        ArgumentOutOfRangeException.ThrowIfNegative(headerPaddingBytes);

        _crc = crc;
        _encryptTable = encryptTable;
        _decryptTable = decryptTable;
        _headerPaddingBytes = headerPaddingBytes;
    }

        public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
    {
        var totalLength = checked(MinHeaderSize + _headerPaddingBytes + plaintext.Length);
        if (totalLength > ushort.MaxValue)
        {
            throw new ArgumentException(
                $"Packet would be {totalLength} bytes; max is {ushort.MaxValue}.",
                nameof(plaintext));
        }

        var packet = new byte[totalLength];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)totalLength);

        var crcInputSpan = packet.AsSpan(MinHeaderSize, _headerPaddingBytes + plaintext.Length);
        plaintext.CopyTo(packet.AsSpan(MinHeaderSize + _headerPaddingBytes));

        var crc = _crc.Compute(crcInputSpan);
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(LengthFieldSize), crc);

        var keyNibble = (byte)((crc >> 8) & 0x0F);
        SubstituteInPlace(packet.AsSpan(MinHeaderSize + _headerPaddingBytes), keyNibble, encrypt: true);

        return packet;
    }

        public byte[] Decrypt(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < MinHeaderSize + _headerPaddingBytes)
        {
            throw new InvalidPacketException(
                $"Frame too short: {frame.Length} bytes; needs at least {MinHeaderSize + _headerPaddingBytes}.");
        }

        var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(frame);
        if (declaredLength != frame.Length)
        {
            throw new InvalidPacketException(
                $"Frame length field says {declaredLength} but buffer is {frame.Length} bytes.");
        }

        var crcOnWire = BinaryPrimitives.ReadUInt16LittleEndian(frame[LengthFieldSize..]);
        var keyNibble = (byte)((crcOnWire >> 8) & 0x0F);

        var plaintextPayload = new byte[frame.Length - MinHeaderSize - _headerPaddingBytes];
        frame[(MinHeaderSize + _headerPaddingBytes)..].CopyTo(plaintextPayload);
        SubstituteInPlace(plaintextPayload, keyNibble, encrypt: false);

        var crcInput = new byte[_headerPaddingBytes + plaintextPayload.Length];
        frame.Slice(MinHeaderSize, _headerPaddingBytes).CopyTo(crcInput);
        plaintextPayload.CopyTo(crcInput.AsSpan(_headerPaddingBytes));

        var expected = _crc.Compute(crcInput);
        if (expected != crcOnWire)
        {
            throw new InvalidPacketException(
                $"CRC mismatch: wire {crcOnWire:X4}, computed {expected:X4}.");
        }

        return plaintextPayload;
    }

    private void SubstituteInPlace(Span<byte> bytes, byte keyNibble, bool encrypt)
    {
        var table = encrypt ? _encryptTable : _decryptTable;
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = table.Substitute(keyNibble, bytes[i]);
        }
    }
}
