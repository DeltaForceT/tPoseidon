using tPoseidon.Core.Common;
using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.Net;

public sealed class PacketWriter
{
    private byte[] _buffer;
    private int _position;

    public PacketWriter(int initialCapacity = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialCapacity);
        _buffer = new byte[initialCapacity];
    }

    public int Position => _position;

    public void WriteByte(byte value)
    {
        Reserve(1);
        _buffer[_position++] = value;
    }

    public void WriteUInt16(ushort value)
    {
        Reserve(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(_position, 2), value);
        _position += 2;
    }

    public void WriteInt16(short value) => WriteUInt16((ushort)value);

    public void WriteUInt32(uint value)
    {
        Reserve(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(_position, 4), value);
        _position += 4;
    }

    public void WriteInt32(int value) => WriteUInt32((uint)value);

    public void WriteInt32Field(long value) => WriteInt32(WireLimits.SaturateInt32(value));

    public void WriteUInt32Field(long value) => WriteUInt32(WireLimits.Int32FieldUnsigned(value));

    public void WriteUInt32Field(ulong value) => WriteUInt32(WireLimits.Int32FieldUnsigned(value));

    public void WriteInt16Field(long value) => WriteInt16(WireLimits.Int16Field(value));

    public void WriteUInt16Field(long value) => WriteUInt16(WireLimits.UInt16Field(value));

    public void WriteUInt64(ulong value)
    {
        Reserve(8);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(_position, 8), value);
        _position += 8;
    }

    public void WriteInt64(long value) => WriteUInt64((ulong)value);

    public void WriteBytes(ReadOnlySpan<byte> data)
    {
        Reserve(data.Length);
        data.CopyTo(_buffer.AsSpan(_position, data.Length));
        _position += data.Length;
    }

        public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount > ushort.MaxValue)
        {
            throw new ArgumentException($"Encoded string is {byteCount} bytes; max u16 length is {ushort.MaxValue}.", nameof(value));
        }
        WriteUInt16((ushort)byteCount);
        Reserve(byteCount);
        Encoding.UTF8.GetBytes(value, _buffer.AsSpan(_position, byteCount));
        _position += byteCount;
    }

        public void WriteFixedString(string value, int byteLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);
        var encoded = Encoding.UTF8.GetByteCount(value);
        if (encoded > byteLength)
        {
            throw new ArgumentException(
                $"String encodes to {encoded} bytes; field is only {byteLength} bytes.",
                nameof(value));
        }
        Reserve(byteLength);
        var slot = _buffer.AsSpan(_position, byteLength);
        slot.Clear();
        Encoding.UTF8.GetBytes(value, slot[..encoded]);
        _position += byteLength;
    }

    public void WriteNulTerminated(string value, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(encoding);
        var bytes = encoding.GetBytes(value);
        if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            throw new ArgumentException("A NUL-terminated string cannot contain a NUL character.", nameof(value));
        }
        WriteBytes(bytes);
        WriteByte(0);
    }

        public void WriteZero(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        Reserve(count);
        _buffer.AsSpan(_position, count).Clear();
        _position += count;
    }

        public byte[] ToArray()
    {
        var copy = new byte[_position];
        Array.Copy(_buffer, copy, _position);
        return copy;
    }

        public void Reset() => _position = 0;

    private void Reserve(int additional)
    {
        var needed = _position + additional;
        if (needed <= _buffer.Length) return;

        var newCap = Math.Max(_buffer.Length * 2, needed);
        var grown = new byte[newCap];
        Array.Copy(_buffer, grown, _position);
        _buffer = grown;
    }
}
