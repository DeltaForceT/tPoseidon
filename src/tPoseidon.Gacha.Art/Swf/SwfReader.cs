using System.Text;

namespace tPoseidon.Gacha.Art.Swf;

public sealed class SwfReader
{
    private readonly byte[] _data;
    private readonly int _end;
    private int _position;
    private int _bitBuffer;
    private int _bitCount;

    public SwfReader(byte[] data)
        : this(data, 0, data?.Length ?? 0)
    {
    }

    public SwfReader(byte[] data, int offset, int length)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (offset < 0 || length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));
        _data = data;
        _position = offset;
        _end = offset + length;
    }

    public int Position => _position;

    public int End => _end;

    public int Remaining => _end - _position;

    public bool AtEnd => _position >= _end;

    public byte[] Buffer => _data;

    public void Align()
    {
        _bitCount = 0;
        _bitBuffer = 0;
    }

    public void Seek(int position)
    {
        if (position < 0 || position > _end) throw new SwfFormatException($"seek to {position} outside 0..{_end}");
        Align();
        _position = position;
    }

    public void Skip(int count) => Seek(_position + count);

    public byte U8()
    {
        Align();
        Need(1);
        return _data[_position++];
    }

    public ushort U16()
    {
        Align();
        Need(2);
        var value = (ushort)(_data[_position] | (_data[_position + 1] << 8));
        _position += 2;
        return value;
    }

    public short S16() => unchecked((short)U16());

    public uint U32()
    {
        Align();
        Need(4);
        var value = (uint)(_data[_position] | (_data[_position + 1] << 8) | (_data[_position + 2] << 16) | (_data[_position + 3] << 24));
        _position += 4;
        return value;
    }

    public double Fixed8() => S16() / 256.0;

    public byte[] Bytes(int count)
    {
        Align();
        Need(count);
        var result = new byte[count];
        Array.Copy(_data, _position, result, 0, count);
        _position += count;
        return result;
    }

    public ReadOnlySpan<byte> Span(int count)
    {
        Align();
        Need(count);
        var span = new ReadOnlySpan<byte>(_data, _position, count);
        _position += count;
        return span;
    }

    public string NullTerminatedString()
    {
        Align();
        var start = _position;
        while (_position < _end && _data[_position] != 0) _position++;
        if (_position >= _end) throw new SwfFormatException("unterminated string");
        var text = Encoding.UTF8.GetString(_data, start, _position - start);
        _position++;
        return text;
    }

    public string FixedString(int length) => Encoding.UTF8.GetString(Span(length));

    public bool Flag() => UBits(1) == 1;

    public uint UBits(int count)
    {
        if (count < 0 || count > 32) throw new SwfFormatException($"bit count {count}");
        uint value = 0;
        for (var i = 0; i < count; i++)
        {
            if (_bitCount == 0)
            {
                Need(1);
                _bitBuffer = _data[_position++];
                _bitCount = 8;
            }
            _bitCount--;
            value = (value << 1) | (uint)((_bitBuffer >> _bitCount) & 1);
        }
        return value;
    }

    public int SBits(int count)
    {
        if (count == 0) return 0;
        var value = UBits(count);
        if (count < 32 && (value & (1u << (count - 1))) != 0) value |= uint.MaxValue << count;
        return unchecked((int)value);
    }

    public double FBits(int count) => SBits(count) / 65536.0;

    private void Need(int count)
    {
        if (_position + count > _end) throw new SwfFormatException($"read of {count} byte(s) at {_position} past end {_end}");
    }
}
