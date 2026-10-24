using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Core.Net.S2;

public sealed class S2StreamFramer
{
    private readonly KeyTable _table;
    private readonly IS2CipherDerivation _derivation;
    private byte[] _buffer = new byte[8192];
    private int _start;
    private int _length;

    public S2StreamFramer(KeyTable table, IS2CipherDerivation derivation, byte initialSessionKey)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(derivation);
        _table = table;
        _derivation = derivation;
        SessionKey = initialSessionKey;
    }

    public byte SessionKey { get; private set; }

    public int Buffered => _length;

        public void OnSessionKeyAdvanced(byte newKey) => SessionKey = newKey;

        public void Feed(ReadOnlySpan<byte> incoming)
    {
        if (incoming.IsEmpty) return;
        EnsureCapacity(_length + incoming.Length);
        incoming.CopyTo(_buffer.AsSpan(_start + _length));
        _length += incoming.Length;
    }

        public bool TryDrainOne(out byte[] frame) => TryDrain(out frame) == S2FrameStatus.Frame;

    public S2FrameStatus TryDrain(out byte[] frame, int maxFrameBytes = ushort.MaxValue)
    {
        frame = Array.Empty<byte>();
        if (_length < S2PacketCipher.MinimumFrameSize) return S2FrameStatus.NeedMore;

        ReadOnlySpan<byte> head = _buffer.AsSpan(_start, _length);
        try
        {
            _derivation.ValidatePackingFlags(head, _table);
        }
        catch (InvalidPacketException)
        {
            return S2FrameStatus.Malformed;
        }

        var len = _derivation.RecoverLength(head, SessionKey, _table);
        if (len < S2PacketCipher.MinimumFrameSize) return S2FrameStatus.Malformed;
        if (len > maxFrameBytes) return S2FrameStatus.Oversized;
        if (_length < len) return S2FrameStatus.NeedMore;

        frame = new byte[len];
        head[..len].CopyTo(frame);
        _start += len;
        _length -= len;
        return S2FrameStatus.Frame;
    }

    public ushort PeekLength() =>
        _length < S2PacketCipher.MinimumFrameSize
            ? (ushort)0
            : _derivation.RecoverLength(_buffer.AsSpan(_start, _length), SessionKey, _table);

    private void Compact()
    {
        if (_start == 0) return;
        if (_length > 0)
        {
            Array.Copy(_buffer, _start, _buffer, 0, _length);
        }
        _start = 0;
    }

    private void EnsureCapacity(int needed)
    {
        if (_start + needed > _buffer.Length) Compact();
        if (needed <= _buffer.Length) return;
        var grown = Math.Max(needed, _buffer.Length * 2);
        Array.Resize(ref _buffer, grown);
    }
}

public enum S2FrameStatus
{
    Frame,
    NeedMore,
    Malformed,
    Oversized,
}
