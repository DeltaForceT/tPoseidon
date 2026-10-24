using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net;

public ref struct PacketReader
{
    private readonly ReadOnlySpan<byte> _buffer;
    private int _position;

    public PacketReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public int Position => _position;
    public int Remaining => _buffer.Length - _position;

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_position++];
    }

    public ushort ReadUInt16()
    {
        EnsureAvailable(2);
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.Slice(_position, 2));
        _position += 2;
        return v;
    }

    public short ReadInt16() => (short)ReadUInt16();

    public uint ReadUInt32()
    {
        EnsureAvailable(4);
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_buffer.Slice(_position, 4));
        _position += 4;
        return v;
    }

    public int ReadInt32() => (int)ReadUInt32();

    public ulong ReadUInt64()
    {
        EnsureAvailable(8);
        var v = BinaryPrimitives.ReadUInt64LittleEndian(_buffer.Slice(_position, 8));
        _position += 8;
        return v;
    }

    public long ReadInt64() => (long)ReadUInt64();

    public byte[] ReadBytes(int count)
    {
        EnsureAvailable(count);
        var result = _buffer.Slice(_position, count).ToArray();
        _position += count;
        return result;
    }

        public string ReadString()
    {
        var byteCount = ReadUInt16();
        EnsureAvailable(byteCount);
        var s = Encoding.UTF8.GetString(_buffer.Slice(_position, byteCount));
        _position += byteCount;
        return s;
    }

        public string ReadFixedString(int byteLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteLength);
        EnsureAvailable(byteLength);
        var raw = _buffer.Slice(_position, byteLength);
        _position += byteLength;
        var trimmedLen = byteLength;
        while (trimmedLen > 0 && raw[trimmedLen - 1] == 0)
        {
            trimmedLen--;
        }
        return Encoding.UTF8.GetString(raw[..trimmedLen]);
    }

    public string ReadNulTerminated(Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var length = _buffer[_position..].IndexOf((byte)0);
        if (length < 0)
        {
            throw new InvalidPacketException($"No NUL terminator after position {_position}.");
        }
        var value = encoding.GetString(_buffer.Slice(_position, length));
        _position += length + 1;
        return value;
    }

        public void Skip(int count)
    {
        EnsureAvailable(count);
        _position += count;
    }

    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
        {
            throw new InvalidPacketException(
                $"Read of {count} bytes at position {_position} exceeds buffer length {_buffer.Length}.");
        }
    }
}
